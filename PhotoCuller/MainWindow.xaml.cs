using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace PhotoCuller;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png",
        ".cr2", ".cr3", ".nef", ".arw", ".dng", ".raf", ".rw2", ".orf", ".srw",
        ".pef", ".sr2", ".nrw", ".rwl", ".x3f", ".3fr", ".mef", ".mos", ".kdc",
        ".erf", ".raw"
    };

    private readonly List<string> _files = new();
    private readonly Stack<MoveRecord> _history = new();
    private int _index;
    private int _initialCount;
    private string? _sourceFolder;
    private string? _trashFolder;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "写真フォルダーを選択してください",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(_sourceFolder))
        {
            dialog.SelectedPath = _sourceFolder;
        }

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        LoadFolder(dialog.SelectedPath);
    }

    private void LoadFolder(string folder)
    {
        _sourceFolder = folder;
        FolderPathText.Text = folder;
        _trashFolder = Path.Combine(folder, "_rejected");
        TrashPathText.Text = $"移動先: {_trashFolder}";

        _history.Clear();
        _files.Clear();
        _files.AddRange(Directory.EnumerateFiles(folder)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        _initialCount = _files.Count;
        _index = 0;
        ShowCurrent();
    }

    private void Keep_Click(object sender, RoutedEventArgs e)
    {
        MoveNext();
    }

    private void Reject_Click(object sender, RoutedEventArgs e)
    {
        MoveCurrentToTrash();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        UndoLastMove();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.K:
            case Key.Right:
            case Key.Space:
                MoveNext();
                e.Handled = true;
                break;
            case Key.D:
            case Key.Delete:
                MoveCurrentToTrash();
                e.Handled = true;
                break;
            case Key.U:
                UndoLastMove();
                e.Handled = true;
                break;
        }
    }

    private void MoveNext()
    {
        if (_files.Count == 0)
        {
            return;
        }

        if (_index < _files.Count - 1)
        {
            _index++;
            ShowCurrent();
        }
    }

    private void MoveCurrentToTrash()
    {
        if (_files.Count == 0 || string.IsNullOrWhiteSpace(_trashFolder))
        {
            return;
        }

        var current = _files[_index];
        if (!File.Exists(current))
        {
            _files.RemoveAt(_index);
            ShowCurrent();
            return;
        }

        Directory.CreateDirectory(_trashFolder);
        var destination = GetUniquePath(Path.Combine(_trashFolder, Path.GetFileName(current)));

        try
        {
            File.Move(current, destination);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"移動に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _history.Push(new MoveRecord(current, destination, _index));
        _files.RemoveAt(_index);

        if (_index >= _files.Count)
        {
            _index = _files.Count - 1;
        }

        ShowCurrent();
    }

    private void UndoLastMove()
    {
        if (_history.Count == 0)
        {
            UpdateButtons();
            return;
        }

        var record = _history.Pop();
        if (!File.Exists(record.MovedPath))
        {
            MessageBox.Show("移動先にファイルが見つかりませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateButtons();
            return;
        }

        var restorePath = record.OriginalPath;
        if (File.Exists(restorePath))
        {
            restorePath = GetUniquePath(restorePath);
        }

        try
        {
            File.Move(record.MovedPath, restorePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"復元に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var insertIndex = Math.Clamp(record.OriginalIndex, 0, _files.Count);
        _files.Insert(insertIndex, restorePath);
        _index = insertIndex;
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (_files.Count == 0)
        {
            ShowEmptyState();
            return;
        }

        _index = Math.Clamp(_index, 0, _files.Count - 1);
        var path = _files[_index];

        if (!File.Exists(path))
        {
            _files.RemoveAt(_index);
            ShowCurrent();
            return;
        }

        FileNameText.Text = Path.GetFileName(path);
        StatusText.Text = $"{_index + 1} / {_files.Count}";

        try
        {
            PreviewImage.Source = LoadPreview(path);
            NoPreviewText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            PreviewImage.Source = null;
            NoPreviewText.Text = $"プレビュー不可: {Path.GetExtension(path).ToLowerInvariant()}";
            NoPreviewText.Visibility = Visibility.Visible;
        }

        UpdateButtons();
    }

    private void ShowEmptyState()
    {
        PreviewImage.Source = null;
        NoPreviewText.Text = _initialCount == 0
            ? "フォルダー内に対象の画像がありません。"
            : "すべて確認しました。";
        NoPreviewText.Visibility = Visibility.Visible;
        FileNameText.Text = string.Empty;
        StatusText.Text = "0 / 0";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var hasFiles = _files.Count > 0;
        KeepButton.IsEnabled = hasFiles;
        RejectButton.IsEnabled = hasFiles;
        UndoButton.IsEnabled = _history.Count > 0;
    }

    private static BitmapImage LoadPreview(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.DecodePixelWidth = 2400;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(directory, $"{fileName}_{i}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("ユニークなファイル名を作成できませんでした。");
    }

    private sealed record MoveRecord(string OriginalPath, string MovedPath, int OriginalIndex);
}
