using QRCoder;

namespace DmCompanion.Screens.Services;

/// <summary>QR de la dirección de la TV como PNG en data URI (sin ficheros ni red).</summary>
public static class TvQrCode
{
    public static string ToPngDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
