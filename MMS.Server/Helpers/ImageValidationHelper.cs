namespace MMS.Server.Helpers
{
    public static class ImageValidationHelper
    {
        private const long MaxFileSize = 20 * 1024 * 1024; // 20 MB

        private static readonly string[] AllowedExtensions =
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif"
        };

        private static readonly string[] AllowedMimeTypes =
        {
            "image/png",
            "image/jpeg",
            "image/bmp",
            "image/gif"
        };

        public static string? ValidateImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return "Nema slike.";

            if (image.Length > MaxFileSize)
                return "Slika je prevelika. Maksimalna veličina je 20 MB.";

            string extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                return "Format slike nije podržan. Dozvoljeni formati su PNG, JPG, JPEG, BMP i GIF.";

            if (!AllowedMimeTypes.Contains(image.ContentType.ToLowerInvariant()))
                return "MIME tip slike nije validan.";

            string fileName = Path.GetFileName(image.FileName);

            if (fileName != image.FileName)
                return "Ime fajla nije validno.";

            return null;
        }
    }
}