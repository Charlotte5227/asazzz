using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiGithubActionsSample;

public sealed class MainViewModel : INotifyPropertyChanged
{
    readonly Random _rng = new();

    int _days = 3;
    bool _syncME;
    bool _syncAllSlots;

    int _initMilitary = 0;
    int _initEconomy = 0;

    string _sumText = "";

    // 変更伝播の再帰防止
    bool _syncGuard;

    public int Days
    {
        get => _days;
        set
        {
            if (value < 1) value = 1;
            if (_days == value) return;
            _days = value;
            OnPropertyChanged();
            RebuildDaysForAllSlots();
        }
    }

    /// <summary>軍事Y↔経済Y：同じ日・同番号の枠を同期</summary>
    public bool SyncME
    {
        get => _syncME;
        set
        {
            if (_syncME == value) return;
            _syncME = value;
            OnPropertyChanged();
            ApplySyncModeInitial();
        }
    }

    /// <summary>全スロット同期：同じ日なら軍事も経済も全枠同じY</summary>
    public bool SyncAllSlots
    {
        get => _syncAllSlots;
        set
        {
            if (_syncAllSlots == value) return;
            _syncAllSlots = value;
            OnPropertyChanged();
            ApplySyncModeInitial();
        }
    }

    public int InitMilitary
    {
        get => _initMilitary;
        set { if (_initMilitary == value) return; _initMilitary = value; OnPropertyChanged(); }
    }

    public int InitEconomy
    {
        get => _initEconomy;
        set { if (_initEconomy == value) return; _initEconomy = value; OnPropertyChanged(); }
    }

    public string SumText
    {
        get => _sumText;
        set { if (_sumText == value) return; _sumText = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SlotModel> MilitarySlots { get; } = new();
    public ObservableCollection<SlotModel> EconomySlots { get; } = new();

    public ObservableCollection<DayResult> Results { get; } = new();

    public MainViewModel()
    {
        AddSlot(SlotKind.Military);
        AddSlot(SlotKind.Economy);
        HookAllYCells();
        SumText = "未生成";
    }

    // ---------------- Slot ops ----------------
    public void AddSlot(SlotKind kind)
    {
        var list = (kind == SlotKind.Military) ? MilitarySlots : EconomySlots;
        list.Add(new SlotModel(kind, list.Count, Days));
        Reindex(kind);

        // 新規スロットのYセル購読
        HookSlotYCells(list.Last());

        // 同期モードがONなら、既存と揃える
        ApplySyncModeInitial();
    }

    public void RemoveSlot(SlotKind kind)
    {
        var list = (kind == SlotKind.Military) ? MilitarySlots : EconomySlots;
        if (list.Count == 0) return;
        list.RemoveAt(list.Count - 1);
        Reindex(kind);
        ApplySyncModeInitial();
    }

    void Reindex(SlotKind kind)
    {
        var list = (kind == SlotKind.Military) ? MilitarySlots : EconomySlots;
        for (int i = 0; i < list.Count; i++)
            list[i].SetIndex(i);
    }

    void RebuildDaysForAllSlots()
    {
        foreach (var s in MilitarySlots) s.EnsureDays(Days);
        foreach (var s in EconomySlots) s.EnsureDays(Days);

        // 変更後も購読が切れないように再購読
        HookAllYCells();

        ApplySyncModeInitial();
    }

    // ---------------- Y sync ----------------
    void HookAllYCells()
    {
        // 既存の購読を二重にしないために、全スロットを一旦解除してから…は重いので、
        // 実用上は「二重購読しない」設計に寄せる（YCellは新規生成されるため、増えた分だけ購読すればOK）
        // ここでは確実性優先で全スロット再Hook（重い処理ではない）
        foreach (var s in MilitarySlots) HookSlotYCells(s);
        foreach (var s in EconomySlots) HookSlotYCells(s);
    }

    void HookSlotYCells(SlotModel slot)
    {
        foreach (var cell in slot.YCells)
        {
            cell.PropertyChanged -= OnYCellChanged;
            cell.PropertyChanged += OnYCellChanged;
        }
        slot.PropertyChanged -= OnSlotPropertyChanged;
        slot.PropertyChanged += OnSlotPropertyChanged;
    }

    void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 増加OFFならそのスロットのYは実質0扱いだが、UIはそのまま（見た目は残す）
        // 生成時に UseIncrease を見て適用する
    }

    void OnYCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(YCell.Value)) return;
        if (_syncGuard) return;

        // どのスロット・どの日が変わったかを特定
        if (sender is not YCell changedCell) return;

        // 探索（スロット数が多くない前提で簡潔に）
        (SlotKind kind, int slotIndex, int dayIndex)? found = FindCell(changedCell);
        if (found is null) return;

        var (kind, si, di) = found.Value;
        var newVal = changedCell.Value;

        _syncGuard = true;
        try
        {
            if (SyncAllSlots)
            {
                // 同じ日を全スロットへ
                foreach (var s in MilitarySlots) SetCellSafe(s, di, newVal);
                foreach (var s in EconomySlots) SetCellSafe(s, di, newVal);
                return;
            }

            if (SyncME)
            {
                // 同じ番号の反対側へ（存在する場合）
                if (kind == SlotKind.Military)
                {
                    if (si < EconomySlots.Count) SetCellSafe(EconomySlots[si], di, newVal);
                }
                else
                {
                    if (si < MilitarySlots.Count) SetCellSafe(MilitarySlots[si], di, newVal);
                }
            }
        }
        finally
        {
            _syncGuard = false;
        }
    }

    (SlotKind kind, int slotIndex, int dayIndex)? FindCell(YCell target)
    {
        for (int si = 0; si < MilitarySlots.Count; si++)
        {
            var s = MilitarySlots[si];
            for (int di = 0; di < s.YCells.Count; di++)
                if (ReferenceEquals(s.YCells[di], target)) return (SlotKind.Military, si, di);
        }
        for (int si = 0; si < EconomySlots.Count; si++)
        {
            var s = EconomySlots[si];
            for (int di = 0; di < s.YCells.Count; di++)
                if (ReferenceEquals(s.YCells[di], target)) return (SlotKind.Economy, si, di);
        }
        return null;
    }

    void SetCellSafe(SlotModel slot, int dayIndex, double value)
    {
        if (dayIndex < 0 || dayIndex >= slot.YCells.Count) return;
        slot.YCells[dayIndex].Value = value;
    }

    void ApplySyncModeInitial()
    {
        if (_syncGuard) return;
        if (!SyncAllSlots && !SyncME) return;

        _syncGuard = true;
        try
        {
            for (int di = 0; di < Days; di++)
            {
                if (SyncAllSlots)
                {
                    // 代表値：軍事1のその日（なければ0）
                    double baseVal = (MilitarySlots.Count > 0) ? MilitarySlots[0].YCells[di].Value : 0.0;
                    foreach (var s in MilitarySlots) SetCellSafe(s, di, baseVal);
                    foreach (var s in EconomySlots) SetCellSafe(s, di, baseVal);
                }
                else if (SyncME)
                {
                    // 軍事を基準に経済へ
                    int n = Math.Min(MilitarySlots.Count, EconomySlots.Count);
                    for (int si = 0; si < n; si++)
                    {
                        double baseVal = MilitarySlots[si].YCells[di].Value;
                        SetCellSafe(EconomySlots[si], di, baseVal);
                    }
                }
            }
        }
        finally
        {
            _syncGuard = false;
        }
    }

    // ---------------- Generate & Sum ----------------
    public void Generate()
    {
        // 入力値の最低限チェック
        foreach (var s in MilitarySlots)
            if (s.MaxValue < 1) s.MaxValue = 1;
        foreach (var s in EconomySlots)
            if (s.MaxValue < 1) s.MaxValue = 1;

        Results.Clear();

        for (int di = 0; di < Days; di++)
        {
            var day = new DayResult { Day = di + 1 };

            // Military
            for (int si = 0; si < MilitarySlots.Count; si++)
            {
                var s = MilitarySlots[si];
                int raw = s.UseSign ? DrawSigned(s.MaxValue) : DrawPlus(s.MaxValue);

                int val = raw;
                if (s.UseIncrease)
                {
                    double y = s.YCells[di].Value;
                    val = ApplyIncreaseRounded(raw, y);
                }
                day.MilitaryValues.Add(val);
            }

            // Economy
            for (int si = 0; si < EconomySlots.Count; si++)
            {
                var s = EconomySlots[si];
                int raw = s.UseSign ? DrawSigned(s.MaxValue) : DrawPlus(s.MaxValue);

                int val = raw;
                if (s.UseIncrease)
                {
                    double y = s.YCells[di].Value;
                    val = ApplyIncreaseRounded(raw, y);
                }
                day.EconomyValues.Add(val);
            }

            Results.Add(day);
        }

        SumText = "生成済み（合算ボタンで最終）";
    }

    public void SumAll()
    {
        if (Results.Count == 0)
        {
            SumText = "先に生成してください";
            return;
        }

        int addM = Results.Sum(r => r.MilitarySum);
        int addE = Results.Sum(r => r.EconomySum);

        int finalM = InitMilitary + addM;
        int finalE = InitEconomy + addE;

        SumText = $"軍事力合計 = {InitMilitary:+#;-#;0} + {addM:+#;-#;0} = {finalM:+#;-#;0}    /    経済力合計 = {InitEconomy:+#;-#;0} + {addE:+#;-#;0} = {finalE:+#;-#;0}";
    }

    // ---------------- math helpers ----------------
    int DrawPlus(int max) => _rng.Next(1, max + 1);

    int DrawSigned(int max)
    {
        int v = _rng.Next(1, max + 1);
        // 1..3 で 1 のときマイナス
        if (_rng.Next(1, 4) == 1) v = -v;
        return v;
    }

    int ApplyIncreaseRounded(int x, double yPercent)
    {
        double val = x + (Math.Abs(x) * (yPercent / 100.0));
        return (int)Math.Round(val, MidpointRounding.AwayFromZero);
    }

    // ---------------- INotifyPropertyChanged ----------------
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
