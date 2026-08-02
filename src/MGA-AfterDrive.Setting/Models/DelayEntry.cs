using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MGA_AfterDrive.Setting.Models;

/// <summary>
/// 遅延実行エントリ。
/// </summary>
public sealed class DelayEntry : INotifyPropertyChanged
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
    /// Google Drive 上のアプリは切断時に強制終了し、復帰時に再起動する。
    /// ユーザーが編集するのではなく、パスが Google Drive 配下かどうかで自動設定される。
    /// </summary>
    public bool Restart
    {
        get => _restart;
        set
        {
            if (!SetField(ref _restart, value))
            {
                return;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RestartMark)));
        }
    }

    /// <summary>
    /// Restart 列の表示用（✓ / 空）。
    /// </summary>
    public string RestartMark => Restart ? "✓" : string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
