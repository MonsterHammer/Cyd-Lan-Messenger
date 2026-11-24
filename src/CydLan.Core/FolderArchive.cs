using System.IO.Compression;

namespace CydLan.Core;

/// <summary>A temporary ZIP sent through the ordinary LAN Messenger file-transfer protocol.</summary>
internal sealed class FolderArchive : IDisposable
{
    private readonly string stagingDirectory;
    public string Path { get; }

    private FolderArchive(string stagingDirectory, string name)
    {
        this.stagingDirectory = stagingDirectory;
        Path = System.IO.Path.Combine(stagingDirectory, name);
    }

    public static async Task<FolderArchive> CreateAsync(DirectoryInfo source, CancellationToken cancellationToken)
    {
        if (!source.Exists) throw new DirectoryNotFoundException("Folder not found.");
        if (source.Parent is null) throw new IOException("A drive root can't be attached.");
        if (source.Name.Length > 236) throw new IOException("The folder name is too long.");
        string tempRelativePath = System.IO.Path.GetRelativePath(source.FullName, System.IO.Path.GetTempPath());
        if (!System.IO.Path.IsPathRooted(tempRelativePath) && tempRelativePath != ".." &&
            !tempRelativePath.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("A folder containing the system temporary directory can't be attached.");
        var archive = new FolderArchive(Directory.CreateTempSubdirectory("CydLan-folder-").FullName, source.Name + ".zip");
        try
        {
            using var zip = ZipFile.Open(archive.Path, ZipArchiveMode.Create);
            var entries = new Stack<FileSystemInfo>();
            entries.Push(source);
            while (entries.TryPop(out var entry))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("The folder contains a symbolic link or junction.");
                if (entry.Name.IndexOfAny(['\\', '/', ':']) >= 0)
                    throw new IOException("The folder contains an unsupported file name.");
                string name = System.IO.Path.GetRelativePath(source.Parent.FullName, entry.FullName)
                    .Replace(System.IO.Path.DirectorySeparatorChar, '/');
                if (entry is DirectoryInfo directory)
                {
                    // Explicit directory entries preserve empty folders too.
                    zip.CreateEntry(name + "/");
                    foreach (var child in directory.EnumerateFileSystemInfos()) entries.Push(child);
                    continue;
                }
                using var input = File.OpenRead(entry.FullName);
                using var output = zip.CreateEntry(name, CompressionLevel.Fastest).Open();
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
            return archive;
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
            Directory.Delete(stagingDirectory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
