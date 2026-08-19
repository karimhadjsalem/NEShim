using System.Linq;

namespace NEShim.UI;

internal sealed partial class InGameMenu
{
    private sealed class SaveSlotSelectHandler : ScreenHandler
    {
        public SaveSlotSelectHandler(InGameMenu menu) : base(menu) { }
        public override string Title =>
            string.Format(Menu._localization.InGameSelectSlotTitle, Menu._saveStates.ActiveSlot + 1);
        public override int ItemCount => Menu._saveStates.SlotCount + 1;
        public override string[] GetItems()
            => Enumerable.Range(0, Menu._saveStates.SlotCount)
                .Select(i => string.Format(Menu._localization.SlotLabel, i + 1)
                           + (i == Menu._saveStates.ActiveSlot ? Menu._localization.SlotActive : ""))
                .Append(Menu._localization.Back)
                .ToArray();
        public override void Activate(int index)
        {
            if (index == Menu._saveStates.SlotCount)
                Menu.NavigateTo(Screen.Root);
            else
            {
                Menu._saveStates.ActiveSlot = index;
                Menu._config.ActiveSlot     = index;
                Menu.NavigateTo(Screen.Root);
            }
        }
    }
}
