using System.ComponentModel;
using System.Runtime.CompilerServices;
using MGA_AfterDrive.IO;

namespace MGA_AfterDrive.Setting.Models;

/// <summary>
/// 遅延実行エントリ。
/// </summary>
public sealed class DelayEntry : INotifyPropertyChanged, IRestartableDelayEntry
{
    private int _delay;
    private string _fileName = string.Empty;
    private string _path = string.Empty;
    private string _option = string.Empty;
    private bool _restart;

    /// <summary>
    /// 起動までの待機時間（秒）。
    /// </summary>
    public int Delay
    {
        get => _delay;
        set => SetField(ref _delay, value);
    }

    public string FileName
    {
        get => _fileName;
        set => SetField(ref _fileName, value ?? string.Empty);
    }

    public string Path
    {
        get => _path;
        set => SetField(ref _path, value ?? string.Empty);
    }

    /// <summary>
    /// 起動時に渡す引数。
    /// </summary>
    public string Option
    {
        get => _option;
        set => SetField(ref _option, value ?? string.Empty);
    }

    /// <summary>
    /// Google Drive 切断時に強制終了し、復帰時に再起動するかどうか。
    /// Google Drive 上のアプリは自動で ON になるが、手動のオンオフも可能。
    /// </summary>
    public bool Restart
    {
        get => _restart;
        set => SetField(ref _restart, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
