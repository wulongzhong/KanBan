using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace KanBan.Services;

public sealed class CardAttachmentService
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    private readonly string _root;

    public CardAttachmentService(JsonBoardStorage storage)
    {
        var boardDirectory = Path.GetDirectoryName(storage.BoardPath)!;
        var boardKey = Path.GetFileNameWithoutExtension(storage.BoardPath);
        _root = Path.Combine(boardDirectory, $"{boardKey}.attachments");
        Directory.CreateDirectory(_root);
    }

    public string ResolveAbsolutePath(string relativePath) =>
        Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return !string.IsNullOrEmpty(extension) &&
               Array.Exists(ImageExtensions, candidate => candidate.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public string SaveImageFromFile(string cardId, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension) || !IsImageFile(sourcePath))
        {
            extension = ".png";
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = GetDestinationPath(cardId, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        return ToRelativePath(cardId, fileName);
    }

    public string SaveImageFromBitmap(string cardId, Bitmap bitmap)
    {
        var fileName = $"{Guid.NewGuid():N}.png";
        var destinationPath = GetDestinationPath(cardId, fileName);
        bitmap.Save(destinationPath);
        return ToRelativePath(cardId, fileName);
    }

    public void DeleteImage(string relativePath)
    {
        var absolutePath = ResolveAbsolutePath(relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    private string GetDestinationPath(string cardId, string fileName)
    {
        var cardDirectory = Path.Combine(_root, cardId);
        Directory.CreateDirectory(cardDirectory);
        return Path.Combine(cardDirectory, fileName);
    }

    private static string ToRelativePath(string cardId, string fileName) => $"{cardId}/{fileName}";
}
