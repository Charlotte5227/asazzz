namespace WarRandomApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        SetTab(isMil:true);
    }

    MainViewModel VM => (MainViewModel)BindingContext;

    void OnGenerateClicked(object sender, EventArgs e)
    {
        VM.Generate();
    }

    void OnSumClicked(object sender, EventArgs e)
    {
        VM.SumAll();
    }

    void OnResetClicked(object sender, EventArgs e)
    {
        VM.Results.Clear();
        VM.SumText = "未生成";
    }

    void OnAddMilitarySlot(object sender, EventArgs e) => VM.AddSlot(SlotKind.Military);
    void OnAddEconomySlot(object sender, EventArgs e) => VM.AddSlot(SlotKind.Economy);
    void OnRemoveMilitarySlot(object sender, EventArgs e) => VM.RemoveSlot(SlotKind.Military);
    void OnRemoveEconomySlot(object sender, EventArgs e) => VM.RemoveSlot(SlotKind.Economy);

    void OnTabMil(object sender, EventArgs e) => SetTab(true);
    void OnTabEco(object sender, EventArgs e) => SetTab(false);

    void SetTab(bool isMil)
    {
        MilPanel.IsVisible = isMil;
        EcoPanel.IsVisible = !isMil;

        // 見た目だけ“選択中感”
        TabMil.Opacity = isMil ? 1.0 : 0.6;
        TabEco.Opacity = isMil ? 0.6 : 1.0;
    }
}
