using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Spectre;

public static class KeyManager
{
    private static readonly byte[] FixedIV = new byte[16]
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
        10, 11, 12, 13, 14, 15
    };

    private static readonly string _x = "51522zzwlwlbb";

    private static readonly RSAParameters _publicKey = new RSAParameters
    {
        Modulus = Convert.FromBase64String("qci92fWCENaXd2SjAiv2pGx03N1PsG/GS8WTllSM4n4Tw4zo+pyXeYEhz85PyMzWlhifyE/qLOeueqQSJwz8DAopeoetVGq4RtSC6xuhYbJ2t+dPJl6kz2nh1++7ov1uVZEuCZtRrgPSX7dHRkMQi5YgvJwYCaVylxtX/bPVdiTH35djNusST9KfWxME38E0TE/QiDv+UMiNxSzM/+y7TnTDL1rvQzDvuLFCgKLp6DdMj3CNc3gfR97HbVBPLo7Q5c28CSPU1PJaXN5uAqh/fyvvAI/N7sxytCKxWE/k4vZB5EKxA/4Xz+hm2wBCpS2wd6er30Wi3AzhY8hY1vGbdw=="),
        Exponent = Convert.FromBase64String("AQAB")
    };

    internal static bool ValidateKey(string key, string currentDate)
    {
        bool debug = false;
        if (debug) return true;
        currentDate = SystemInfo.deviceUniqueIdentifier + "-" + currentDate;
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(currentDate);
            byte[] inArray = SHA256.Create().ComputeHash(bytes);
            string key2 = Convert.ToBase64String(inArray);
            byte[] data = EncryptWithAes(_x, key2);
            byte[] signature = Convert.FromBase64String(key);
            using RSA rSA = RSA.Create();
            rSA.ImportParameters(_publicKey);
            return rSA.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
            return false;
        }
    }

    private static byte[] EncryptWithAes(string plainText, string key)
    {
        using Aes aes = Aes.Create();
        aes.Key = Convert.FromBase64String(key);
        aes.IV = FixedIV;
        ICryptoTransform cryptoTransform = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] bytes = Encoding.UTF8.GetBytes(plainText);
        byte[] array = cryptoTransform.TransformFinalBlock(bytes, 0, bytes.Length);
        byte[] array2 = new byte[aes.IV.Length + array.Length];
        Array.Copy(aes.IV, 0, array2, 0, aes.IV.Length);
        Array.Copy(array, 0, array2, aes.IV.Length, array.Length);
        return array2;
    }

    internal static string TryGetInternetTime()
    {
        try
        {
            string host = "time.windows.com";
            byte[] array = new byte[48];
            array[0] = 27;
            using (RNGCryptoServiceProvider rNGCryptoServiceProvider = new RNGCryptoServiceProvider())
            {
                byte[] array2 = new byte[4];
                rNGCryptoServiceProvider.GetBytes(array2);
                Array.Copy(array2, 0, array, 44, 4);
            }
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.ReceiveTimeout = 1000;
                socket.Connect(host, 123);
                socket.Send(array);
                socket.Receive(array);
            }
            byte[] array3 = new byte[4];
            Array.Copy(array, 44, array3, 0, 4);
            int num = BitConverter.ToInt32(array3, 0);
            int num2 = BitConverter.ToInt32(array, 44);
            if (num != num2)
            {
                Debug.Log("数据包被篡改，随机数不匹配！");
                return "";
            }
            uint num3 = 0u;
            for (int i = 0; i <= 3; i++)
            {
                num3 = 256 * num3 + array[40 + i];
            }
            DateTime dateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(num3).ToLocalTime();
            Console.WriteLine(dateTime);
            return dateTime.ToString("yyyy-MM-dd");
        }
        catch (SocketException ex)
        {
            Console.WriteLine("网络错误或服务器不可达: " + ex.Message);
            return "";
        }
        catch (Exception ex2)
        {
            Console.WriteLine("获取互联网时间时出错: " + ex2.Message);
            return "";
        }
    }
}
