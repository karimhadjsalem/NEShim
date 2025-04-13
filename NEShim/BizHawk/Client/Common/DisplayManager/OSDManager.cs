using System.Linq;
using System.Text;
using System.Drawing;
using System.Collections.Generic;

using BizHawk.Emulation.Common;

namespace BizHawk.Client.Common
{
	public class OSDManager
	{
		private Config _config;

		private IEmulator _emulator;

		private readonly InputManager _inputManager;

		public OSDManager(Config config, IEmulator emulator, InputManager inputManager)
		{
			_config = config;
			_emulator = emulator;
			_inputManager = inputManager;
		}

		public void UpdateGlobals(Config config, IEmulator emulator)
		{
			_config = config;
			_emulator = emulator;
		}

		public string Fps { get; set; }

		public Color FixedMessagesColor => Color.FromArgb(_config.MessagesColor);
		public Color FixedAlertMessageColor => Color.FromArgb(_config.AlertMessageColor);



		private static Point GetCoordinates(IBlitter g, MessagePosition position, string message)
		{
			var size = g.MeasureString(message);
			var x = position.Anchor.IsLeft()
				? position.X * g.Scale
				: g.ClipBounds.Width - position.X * g.Scale - size.Width;

			var y = position.Anchor.IsTop()
				? position.Y * g.Scale
				: g.ClipBounds.Height - position.Y * g.Scale - size.Height;

			return new Point((int)Math.Round(x), (int)Math.Round(y));
		}

		private readonly List<UIMessage> _messages = new(5);
		private readonly List<UIDisplay> _guiTextList = [ ];
		private readonly List<UIDisplay> _ramWatchList = [ ];

		/// <summary>Clears the queue used by <see cref="AddMessage"/>. You probably don't want to do this.</summary>
		public void ClearRegularMessages()
			=> _messages.Clear();

		public void AddMessage(string message, int? duration = null)
			=> _messages.Add(new() {
				Message = message,
				ExpireAt = DateTime.Now + TimeSpan.FromSeconds(Math.Max(_config.OSDMessageDuration, duration ?? 0)),
			});

		public void ClearRamWatches()
			=> _ramWatchList.Clear();

		public void AddRamWatch(string message, MessagePosition pos, Color backGround, Color foreColor)
		{
			_ramWatchList.Add(new UIDisplay
			{
				Message = message,
				Position = pos,
				BackGround = backGround,
				ForeColor = foreColor
			});
		}

		public void AddGuiText(string message, MessagePosition pos, Color backGround, Color foreColor)
		{
			_guiTextList.Add(new UIDisplay
			{
				Message = message,
				Position = pos,
				BackGround = backGround,
				ForeColor = foreColor
			});
		}

		public void ClearGuiText()
			=> _guiTextList.Clear();

		private void DrawMessage(IBlitter g, UIMessage message, int yOffset)
		{
			var point = GetCoordinates(g, _config.Messages, message.Message);
			var y = point.Y + yOffset; // TODO: clean me up
			g.DrawString(message.Message, FixedMessagesColor, point.X, y);
		}

		public void DrawMessages(IBlitter g)
		{
			if (!_config.DisplayMessages)
			{
				return;
			}

			_messages.RemoveAll(m => DateTime.Now > m.ExpireAt);

			if (_messages.Count is not 0)
			{
				if (_config.StackOSDMessages)
				{
					var line = 1;
					for (var i = _messages.Count - 1; i >= 0; i--, line++)
					{
						var yOffset = (int)Math.Round((line - 1) * 18 * g.Scale);
						if (!_config.Messages.Anchor.IsTop())
						{
							yOffset = 0 - yOffset;
						}

						DrawMessage(g, _messages[i], yOffset);
					}
				}
				else
				{
					var message = _messages[^1];
					DrawMessage(g, message, 0);
				}
			}

			foreach (var text in _guiTextList.Concat(_ramWatchList))
			{
				try
				{
					var point = GetCoordinates(g, text.Position, text.Message);
					if (point.Y >= g.ClipBounds.Height) continue; // simple optimisation; don't bother drawing off-screen
					g.DrawString(text.Message, text.ForeColor, point.X, point.Y);
				}
				catch (Exception)
				{
					return;
				}
			}
		}

		private static string MakeStringFor(IController controller)
		{
			return Bk2InputDisplayGenerator.Generate(controller);
		}

		private static void DrawOsdMessage(IBlitter g, string message, Color color, int x, int y)
			=> g.DrawString(message, color, x, y);

		/// <summary>
		/// Display all screen info objects like fps, frame counter, lag counter, and input display
		/// </summary>
		public void DrawScreenInfo(IBlitter g)
		{
			if (_config.DisplayFps && Fps != null)
			{
				var point = GetCoordinates(g, _config.Fps, Fps);
				DrawOsdMessage(g, Fps, FixedMessagesColor, point.X, point.Y);
			}

			if (_config.DisplayLagCounter && _emulator.CanPollInput())
			{
				var counter = _emulator.AsInputPollable().LagCount.ToString();
				var point = GetCoordinates(g, _config.LagCounter, counter);
				DrawOsdMessage(g, counter, FixedAlertMessageColor, point.X, point.Y);
			}

			if (_inputManager.ClientControls["Autohold"] || _inputManager.ClientControls["Autofire"])
			{
				var sb = new StringBuilder("Held: ");

				foreach (var sticky in _inputManager.StickyHoldController.CurrentHolds)
				{
					sb.Append(sticky).Append(' ');
				}

				foreach (var autoSticky in _inputManager.StickyAutofireController.CurrentAutofires)
				{
					sb
						.Append("Auto-")
						.Append(autoSticky)
						.Append(' ');
				}

				var message = sb.ToString();
				var point = GetCoordinates(g, _config.Autohold, message);
				g.DrawString(message, Color.White, point.X, point.Y);
			}
		}
	}
}
