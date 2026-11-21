using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OpenCiv2.IO;

namespace OpenCiv2.App;

/// <summary>Desktop file capabilities. File handles and streams remain owned until asynchronous completion.</summary>
public sealed class AvaloniaFileTransfer(Func<TopLevel?> owner) : IFileTransfer
{
    private IStorageProvider Provider => owner()?.StorageProvider ?? throw new IOException("File selection is unavailable");
    public async Task<StoredDocument?> ImportAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var provider = Provider;
        if (!provider.CanOpen)
        {
            throw new IOException("This platform cannot select files");
        }

        var files = await provider.OpenFilePickerAsync(new() { Title = "Import a file", AllowMultiple = false });
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (files.Count == 0)
            {
                return null;
            }

            await using var input = await files[0].OpenReadAsync();
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[65536];
            int count;
            while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
            {
                if (bytes.Length + count > StoredDocument.MaximumBytes)
                {
                    throw new IOException("Files must be at most 32 MiB");
                }

                bytes.Write(buffer, 0, count);
            }
            return new(files[0].Name, bytes.ToArray());
        }
        finally
        {
            foreach (var file in files)
            {
                file.Dispose();
            }
        }
    }
    public async Task<ExportResult> ExportAsync(StoredDocument document)
    {
        var provider = Provider;
        if (!provider.CanSave)
        {
            throw new IOException("This platform cannot export files");
        }

        using var file = await provider.SaveFilePickerAsync(new()
        {
            Title = "Export stored file",
            SuggestedFileName = document.Name,
            ShowOverwritePrompt = true
        });
        if (file is null)
        {
            return ExportResult.Cancelled;
        }

        await using (var output = await file.OpenWriteAsync())
        {
            // Some providers open an existing stream without truncation.
            if (output.CanSeek)
            {
                output.SetLength(0);
            }

            await output.WriteAsync(document.Bytes.ToArray());
            await output.FlushAsync();
        }
        return ExportResult.Written;
    }
}
