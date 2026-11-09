using System;

namespace UndoHistory
{
	/// An undo step for the Sanctuary tools (props, texture layers, playable
	/// area): `Capture` takes a snapshot of the state and returns the action
	/// that puts it back. Undo first captures the current state for redo.
	public class HistorySanctuaryState : HistoryObject
	{
		public class Param : HistoryParameter
		{
			public readonly Func<Action> Capture;
			public Param(Func<Action> capture) { Capture = capture; }
		}

		Func<Action> capture;
		Action restore;

		public static void Register(Func<Action> capture)
		{
			Undo.RegisterUndo(new HistorySanctuaryState(), new Param(capture));
		}

		public override void Register(HistoryParameter Params)
		{
			capture = ((Param)Params).Capture;
			restore = capture();
			UndoCommandName = "Sanctuary edit";
		}

		public override void DoUndo()
		{
			if (!RedoGenerated)
				Undo.RegisterRedo(new HistorySanctuaryState(), new Param(capture));
			RedoGenerated = true;
			DoRedo();
		}

		public override void DoRedo()
		{
			restore?.Invoke();
		}
	}
}
