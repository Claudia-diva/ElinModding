using System;

namespace ElonaKeys
{
	// Generic "select one item from your inventory" flow reusing the game's
	// native LayerDragGrid UI (same as offering/identify). Valid items get the
	// guide highlight; clicking one calls onSelect and the layer closes (count=1).
	public class InvOwnerSelectItem : InvOwnerDraglet
	{
		private readonly Func<Thing, bool> filter;
		private readonly Action<Thing> onSelect;
		private readonly string prompt;

		public InvOwnerSelectItem(string _prompt, Func<Thing, bool> _filter, Action<Thing> _onSelect)
			: base(EClass.pc, null, CurrencyType.None)
		{
			prompt = _prompt;
			filter = _filter;
			onSelect = _onSelect;
			count = 1;
		}

		public override bool SingleTarget => true;

		public override void OnInit()
		{
			if (!string.IsNullOrEmpty(prompt))
			{
				Msg.SayRaw(prompt);
			}
		}

		public override bool ShouldShowGuide(Thing t)
		{
			try
			{
				return filter(t);
			}
			catch
			{
				return false;
			}
		}

		public override void _OnProcess(Thing t)
		{
			try
			{
				onSelect(t);
			}
			catch (Exception ex)
			{
				Plugin.Log?.LogError("ElonaKeys item action failed: " + ex);
			}
		}
	}
}
