using Avalonia.Input;

namespace KanBan.Services;

public static class CardImageDropHelper
{
    public static bool CanAccept(IDataTransfer? dataTransfer)
    {
        if (dataTransfer is null)
        {
            return false;
        }

        return dataTransfer.Contains(DataFormat.File) || dataTransfer.Contains(DataFormat.Bitmap);
    }

    public static bool CanAccept(IAsyncDataTransfer? dataTransfer)
    {
        if (dataTransfer is null)
        {
            return false;
        }

        return dataTransfer.Contains(DataFormat.File) || dataTransfer.Contains(DataFormat.Bitmap);
    }
}
