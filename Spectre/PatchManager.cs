using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace Spectre;

internal static class PatchManager
{
    private static Harmony _harmony;
    private static string _harmonyId;
    private static readonly Dictionary<string, object> _delegateCache = new();
    private static readonly Dictionary<string, MethodInfo> _methodCache = new();
    private static readonly Dictionary<string, FieldInfo> _fieldCache = new();
    private static readonly Dictionary<Type, PatchRegistration> _registeredPatches = new();
    private static readonly HashSet<Type> _appliedPatches = new();
    private static readonly HashSet<Type> _failedPatches = new();
    private static readonly List<MethodInfo> _appliedManualPatches = new();
    private static readonly object _lock = new();

    public static void Initialize(Harmony harmony)
    {
        lock (_lock)
        {
            _harmony = harmony;
            _harmonyId = harmony.Id;
            _registeredPatches.Clear();
            _appliedPatches.Clear();
            _failedPatches.Clear();
            _delegateCache.Clear();
            _methodCache.Clear();
            _fieldCache.Clear();
        }
    }

    public static void RegisterPatch(Type patchType, Func<bool> toggle = null)
    {
        lock (_lock)
        {
            _registeredPatches[patchType] = new PatchRegistration(patchType, toggle ?? (() => true));
        }
        Debug.Log($"[PatchManager] Registered patch: {patchType.Name}");
    }

    public static void RegisterPatches(Func<bool> toggle, params Type[] patchTypes)
    {
        foreach (var patchType in patchTypes)
            RegisterPatch(patchType, toggle);
    }

    public static void RegisterLazyPatches(Func<bool> trigger, params Type[] patchTypes)
    {
        lock (_lock)
        {
            foreach (var patchType in patchTypes)
                _registeredPatches[patchType] = new PatchRegistration(patchType, () => true, trigger);
        }
        Debug.Log($"[PatchManager] Registered lazy patches: {string.Join(", ", patchTypes.Select(t => t.Name))}");
    }

    public static void RegisterManualPrefix(MethodInfo targetMethod, MethodInfo prefixMethod)
    {
        if (targetMethod == null || prefixMethod == null) return;
        lock (_lock)
        {
            _harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
            _appliedManualPatches.Add(targetMethod);
        }
        Debug.Log($"Applied manual prefix patch: {targetMethod.DeclaringType.Name}.{targetMethod.Name} -> {prefixMethod.Name}");
    }

    public static void RegisterManualPatch(MethodInfo targetMethod, MethodInfo postfixMethod)
    {
        if (targetMethod == null || postfixMethod == null) return;
        lock (_lock)
        {
            _harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            _appliedManualPatches.Add(targetMethod);
        }
        Debug.Log($"Applied manual patch: {targetMethod.DeclaringType.Name}.{targetMethod.Name} -> {postfixMethod.Name}");
    }

    public static void UnpatchManualPrefix(MethodInfo targetMethod)
    {
        if (targetMethod == null) return;
        lock (_lock)
        {
            _harmony.Unpatch(targetMethod, HarmonyPatchType.Prefix, _harmonyId);
            _appliedManualPatches.Remove(targetMethod);
        }
        Debug.Log($"Unpatched manual prefix: {targetMethod.DeclaringType.Name}.{targetMethod.Name}");
    }

    public static void ApplyAll()
    {
        lock (_lock)
        {
            if (_harmony == null) return;
            foreach (var registration in _registeredPatches.Values)
            {
                if (_appliedPatches.Contains(registration.PatchType)) continue;
                if (!registration.IsEnabled()) continue;
                if (registration.IsLazy) continue;
                try
                {
                    _harmony.CreateClassProcessor(registration.PatchType).Patch();
                    _appliedPatches.Add(registration.PatchType);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to apply patch {registration.PatchType.Name}: {e.Message}");
                }
            }
        }
    }

    public static void ApplyAllAsync()
    {
        System.Threading.CancellationTokenSource cts;
        lock (_lock)
        {
            if (_lazyPollCts != null) return;
            cts = _lazyPollCts = new System.Threading.CancellationTokenSource();
        }
        _ = Task.Run(() => ApplyAllAsyncCore(cts));
    }

    private static async Task ApplyAllAsyncCore(System.Threading.CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            ApplyImmediatePatches();
            while (!token.IsCancellationRequested)
            {
                if (ApplyLazyPatches())
                    break;
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogError($"[PatchManager] Async patch failed: {ex}");
        }
        finally
        {
            lock (_lock)
            {
                if (_lazyPollCts == cts)
                    _lazyPollCts = null;
            }
            cts.Dispose();
        }
    }

    private static void ApplyImmediatePatches()
    {
        lock (_lock)
        {
            if (_harmony == null) return;
            foreach (var registration in _registeredPatches.Values)
            {
                if (registration.IsLazy) continue;
                if (_appliedPatches.Contains(registration.PatchType)) continue;
                if (_failedPatches.Contains(registration.PatchType)) continue;
                if (!registration.IsEnabled()) continue;
                try
                {
                    _harmony.CreateClassProcessor(registration.PatchType).Patch();
                    _appliedPatches.Add(registration.PatchType);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to apply patch {registration.PatchType.Name}: {e.Message}");
                    _failedPatches.Add(registration.PatchType);
                }
            }
        }
    }

    private static bool ApplyLazyPatches()
    {
        lock (_lock)
        {
            if (_harmony == null) return true;
            bool allApplied = true;
            foreach (var registration in _registeredPatches.Values)
            {
                if (!registration.IsLazy) continue;
                if (_failedPatches.Contains(registration.PatchType)) continue;
                bool isApplied = _appliedPatches.Contains(registration.PatchType);
                bool shouldBeEnabled = registration.IsEnabled() && registration.LazyTrigger();
                if (shouldBeEnabled && !isApplied)
                {
                    try
                    {
                        _harmony.CreateClassProcessor(registration.PatchType).Patch();
                        _appliedPatches.Add(registration.PatchType);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to apply patch {registration.PatchType.Name}: {e.Message}");
                        _failedPatches.Add(registration.PatchType);
                    }
                }
                if (!_appliedPatches.Contains(registration.PatchType) && !_failedPatches.Contains(registration.PatchType))
                    allApplied = false;
            }
            return allApplied;
        }
    }

    private static System.Threading.CancellationTokenSource _lazyPollCts;

    public static void UnpatchAll()
    {
        System.Threading.CancellationTokenSource cts;
        lock (_lock)
        {
            cts = _lazyPollCts;
            _lazyPollCts = null;
        }
        cts?.Cancel();
        lock (_lock)
        {
            _harmony?.UnpatchAll(_harmonyId);
            _appliedPatches.Clear();
            _failedPatches.Clear();
            _appliedManualPatches.Clear();
        }
    }

    public static MethodInfo GetMethodInfo(Type declaringType, string methodName, Type[] parameters = null, Type[] generics = null)
    {
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("Method name cannot be empty", nameof(methodName));

        string key = $"{declaringType.FullName}.{methodName}";
        if (parameters != null)
            key += "_" + string.Join(",", parameters.Select(t => t.FullName));
        if (generics != null)
            key += "_generic_" + string.Join(",", generics.Select(t => t.FullName));

        lock (_lock)
        {
            if (_methodCache.TryGetValue(key, out var cached))
                return cached;

            MethodInfo method;
            if (parameters != null)
                method = AccessTools.Method(declaringType, methodName, parameters, generics);
            else
                method = AccessTools.Method(declaringType, methodName, generics);

            if (method == null)
                throw new MissingMethodException($"Cannot find method {methodName} in {declaringType}");

            _methodCache[key] = method;
            return method;
        }
    }

    public static FieldInfo GetFieldInfo(Type declaringType, string fieldName)
    {
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("Field name cannot be empty", nameof(fieldName));

        string key = $"{declaringType.FullName}.{fieldName}";

        lock (_lock)
        {
            if (_fieldCache.TryGetValue(key, out var cached))
                return cached;

            var field = AccessTools.Field(declaringType, fieldName);
            if (field == null)
                throw new MissingFieldException($"Cannot find field {fieldName} in {declaringType}");

            _fieldCache[key] = field;
            return field;
        }
    }

    public static AccessTools.FieldRef<T, F> CreateFieldRef<T, F>(string fieldName) where T : class
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be empty", nameof(fieldName));

        var key = $"Field:{typeof(T).FullName}.{fieldName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (AccessTools.FieldRef<T, F>)cached;

            var fieldRef = AccessTools.FieldRefAccess<T, F>(fieldName);
            _delegateCache[key] = fieldRef;
            return fieldRef;
        }
    }

    public static Func<T, F> CreatePropertyGetter<T, F>(string propertyName) where T : class
    {
        var key = $"PropGet:{typeof(T).FullName}.{propertyName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<T, F>)cached;

            var prop = AccessTools.Property(typeof(T), propertyName);
            if (prop == null) throw new MissingMemberException($"Property '{propertyName}' not found on {typeof(T)}");
            var getMethod = prop.GetGetMethod(true);
            if (getMethod == null) throw new InvalidOperationException($"Property '{propertyName}' has no getter");

            var del = (Func<T, F>)Delegate.CreateDelegate(typeof(Func<T, F>), getMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Action<T, F> CreatePropertySetter<T, F>(string propertyName) where T : class
    {
        var key = $"PropSet:{typeof(T).FullName}.{propertyName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Action<T, F>)cached;

            var prop = AccessTools.Property(typeof(T), propertyName);
            if (prop == null) throw new MissingMemberException($"Property '{propertyName}' not found on {typeof(T)}");
            var setMethod = prop.GetSetMethod(true);
            if (setMethod == null) throw new InvalidOperationException($"Property '{propertyName}' has no setter");

            var del = (Action<T, F>)Delegate.CreateDelegate(typeof(Action<T, F>), setMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Func<TField> CreateStaticFieldGetter<TField>(Type declaringType, string fieldName)
    {
        var key = $"StaticFieldGet:{declaringType.FullName}.{fieldName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<TField>)cached;

            var fi = AccessTools.Field(declaringType, fieldName);
            if (fi == null) throw new MissingMemberException($"{declaringType}.{fieldName}");
            if (!fi.IsStatic) throw new ArgumentException("Field is not static");

            var method = new DynamicMethod($"get_{fieldName}", typeof(TField), Type.EmptyTypes, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, fi);
            il.Emit(OpCodes.Ret);
            var del = (Func<TField>)method.CreateDelegate(typeof(Func<TField>));
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Action<TField> CreateStaticFieldSetter<TField>(Type declaringType, string fieldName)
    {
        var key = $"StaticFieldSet:{declaringType.FullName}.{fieldName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Action<TField>)cached;

            var fi = AccessTools.Field(declaringType, fieldName);
            if (fi == null) throw new MissingMemberException($"{declaringType}.{fieldName}");
            if (!fi.IsStatic) throw new ArgumentException("Field is not static");

            var method = new DynamicMethod($"set_{fieldName}", typeof(void), new[] { typeof(TField) }, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, fi);
            il.Emit(OpCodes.Ret);
            var del = (Action<TField>)method.CreateDelegate(typeof(Action<TField>));
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Func<TField> CreateStaticPropertyGetter<TField>(Type declaringType, string propertyName)
    {
        var key = $"StaticPropGet:{declaringType.FullName}.{propertyName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Func<TField>)cached;

            var prop = AccessTools.Property(declaringType, propertyName);
            if (prop == null) throw new MissingMemberException($"{declaringType}.{propertyName}");
            var getMethod = prop.GetGetMethod(true);
            if (getMethod == null) throw new InvalidOperationException("Property has no getter");

            var del = (Func<TField>)Delegate.CreateDelegate(typeof(Func<TField>), getMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    public static Action<TField> CreateStaticPropertySetter<TField>(Type declaringType, string propertyName)
    {
        var key = $"StaticPropSet:{declaringType.FullName}.{propertyName}";
        lock (_lock)
        {
            if (_delegateCache.TryGetValue(key, out var cached))
                return (Action<TField>)cached;

            var prop = AccessTools.Property(declaringType, propertyName);
            if (prop == null) throw new MissingMemberException($"{declaringType}.{propertyName}");
            var setMethod = prop.GetSetMethod(true);
            if (setMethod == null) throw new InvalidOperationException("Property has no setter");

            var del = (Action<TField>)Delegate.CreateDelegate(typeof(Action<TField>), setMethod);
            _delegateCache[key] = del;
            return del;
        }
    }

    private class PatchRegistration
    {
        public Type PatchType { get; }
        public Func<bool> IsEnabled { get; }
        public Func<bool> LazyTrigger { get; }
        public bool IsLazy => LazyTrigger != null;

        public PatchRegistration(Type patchType, Func<bool> isEnabled, Func<bool> lazyTrigger = null)
        {
            PatchType = patchType;
            IsEnabled = isEnabled;
            LazyTrigger = lazyTrigger;
        }
    }
}
