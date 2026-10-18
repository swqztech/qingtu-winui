using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Qingtu.Helpers;
using Qingtu.Models;
using Qingtu.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Qingtu;

public sealed partial class MainPage : Page
{
    private readonly List<string> _files = [];
    private bool _isCompressTab = true;
    private bool _busy;

    public MainPage()
    {
        InitializeComponent();
        UpdateTabUi();
        UpdateRunLabel();
    }

    private void OnTabCompress(object sender, RoutedEventArgs e)
    {
        _isCompressTab = true;
        UpdateTabUi();
    }

    private void OnTabStitch(object sender, RoutedEventArgs e)
    {
        _isCompressTab = false;
        UpdateTabUi();
    }

    private void UpdateTabUi()
    {
        PanelCompress.Visibility = _isCompressTab ? Visibility.Visible : Visibility.Collapsed;
        TxtStitchHint.Visibility = _isCompressTab ? Visibility.Collapsed : Visibility.Visible;
        UpdateRunLabel();
    }

    private void UpdateRunLabel() =>
        BtnRun.Content = _isCompressTab ? $"开始压缩 ({_files.Count})" : $"开始拼接 ({_files.Count})";

    private void OnQualityChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        TxtQuality.Text = ((int)SliderQuality.Value).ToString();

    private async void OnPickFiles(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".bmp");
        PickerHelper.InitPicker(picker);
        var files = await picker.PickMultipleFilesAsync();
        if (files is null) return;
        AddFiles(files.Select(f => f.Path));
    }

    private async void OnPickOutDir(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        PickerHelper.InitPicker(picker);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        TxtOutDir.Text = folder.Path;
        ChkCustomOut.IsChecked = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
            e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        AddFiles(items.OfType<StorageFile>().Select(f => f.Path));
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (!ImagePaths.IsImageFile(p) || _files.Contains(p)) continue;
            _files.Add(p);
            FileList.Items.Add(Path.GetFileName(p));
        }
        TxtDropHint.Text = _files.Count > 0 ? $"已选 {_files.Count} 个文件" : "拖拽图片到这里";
        TxtDropHint.Foreground = _files.Count > 0
            ? (Brush)Resources["TealBrush"]
            : (Brush)Resources["MutedBrush"];
        UpdateRunLabel();
    }

    private JobOptions BuildOptions()
    {
        _ = int.TryParse(TxtMaxSide.Text.Trim(), out var maxSide);
        if (maxSide < 0) maxSide = 0;
        return new JobOptions
        {
            Quality = (int)SliderQuality.Value,
            MaxSide = maxSide,
            Format = GetFormat(),
            OutputDir = ChkCustomOut.IsChecked == true && !string.IsNullOrWhiteSpace(TxtOutDir.Text)
                ? TxtOutDir.Text.Trim()
                : null
        };
    }

    private OutputFormat GetFormat()
    {
        if (FmtJpeg.IsChecked == true) return OutputFormat.Jpeg;
        if (FmtPng.IsChecked == true) return OutputFormat.Png;
        if (FmtWebp.IsChecked == true) return OutputFormat.WebP;
        return OutputFormat.KeepOriginal;
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_files.Count == 0)
        {
            TxtLog.Text = "请先添加图片";
            return;
        }

        _busy = true;
        BtnRun.IsEnabled = false;
        try
        {
            if (_isCompressTab)
            {
                var opts = BuildOptions();
                var results = await Task.Run(() => ImageCompressService.RunJobs(_files, opts));
                TxtLog.Text = FormatResults.FormatLog(results);
            }
            else
            {
                if (_files.Count < 2)
                {
                    TxtLog.Text = "拼接需要至少 2 张图";
                    return;
                }
                var outPath = await Task.Run(() =>
                {
                    var opts = BuildOptions();
                    var path = !string.IsNullOrWhiteSpace(opts.OutputDir)
                        ? Path.Combine(opts.OutputDir!, "stitched.png")
                        : Path.Combine(ImagePaths.DefaultOutputDir(_files[0]), "stitched.png");
                    ImageStitchService.StitchHorizontal(_files, path);
                    return path;
                });
                TxtLog.Text = $"拼接完成 → {outPath}";
            }
        }
        catch (Exception ex)
        {
            TxtLog.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            BtnRun.IsEnabled = true;
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _files.Clear();
        FileList.Items.Clear();
        TxtLog.Text = "";
        TxtDropHint.Text = "拖拽图片到这里";
        TxtDropHint.Foreground = (Brush)Resources["MutedBrush"];
        UpdateRunLabel();
    }
}
