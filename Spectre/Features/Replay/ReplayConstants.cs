namespace Spectre.Features.Replay;

internal static class ReplayConstants
{
    public const ulong Magic = 0x6C70_7263_5250_53;      // "SPRrcplp"
    public const ulong LegacyMagic = 0x6C70_7263_5052_43; // "CRPrcplp" (pre-Spectre)
    public const int FormatVersion = 1;

    public const ulong MagicMetadata = 0x61_7461_6461_7465_4D;
    public const int MetadataVersion = 1;

    public const ulong MagicKeyEvent = 0x74_6E65_7645_7965_4B;
    public const int KeyEventVersion = 1;

    public const ulong MagicHitContext = 0x74_7865_7443_7469_48;
    public const int HitContextVersion = 1;

    public const string Extension = ".sprp";
    public const string ExtensionNoEncrypt = ".psprp";
}
