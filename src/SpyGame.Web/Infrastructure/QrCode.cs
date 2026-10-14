using QRCoder;

namespace SpyGame.Web.Infrastructure;

public static class QrCode
{
    /// <summary>A black-on-white PNG of <paramref name="text"/>, ready for an img src.</summary>
    public static string PngDataUrl(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
