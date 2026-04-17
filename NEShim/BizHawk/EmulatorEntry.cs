using BizHawk.Client.Common;

namespace BizHawk;

public class EmulatorEntry
{
    public void OpenRom(string filePath, string fileName)
    {
        var romFullPath = new FileInfo(Path.Combine(filePath, fileName)).FullName;
        _ = LoadRom(filePath, new LoadRomArgs(new OpenAdvanced_OpenRom(romFullPath)));
    }

    private bool LoadRom(string path, LoadRomArgs args) => LoadRom(path, args, out _);

    private bool LoadRom(string path, LoadRomArgs args, out bool failureIsFromAskSave)
    {
        /*
        if (!LoadRomInternal(path, args, out failureIsFromAskSave))
            return false;

        // what's the meaning of the last rom path when opening an archive? based on the archive file location
        if (args.OpenAdvanced is OpenAdvanced_OpenRom)
        {
            var leftPart = path.Split('|')[0];
            Config.PathEntries.LastRomPath = Path.GetFullPath(Path.GetDirectoryName(leftPart) ?? "");
        }
        */

 
        //temp
        failureIsFromAskSave = true;
        return true;
    }
    
    
  /*  
    // Still needs a good bit of refactoring
		private bool LoadRomInternal(string path, LoadRomArgs args, out bool failureIsFromAskSave)
		{
			failureIsFromAskSave = false;
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			if (args == null)
				throw new ArgumentNullException(nameof(args));

			_isLoadingRom = true;
			path = EmuHawkUtil.ResolveShortcut(path);

			// if this is the first call to LoadRom (they will come in recursively) then stash the args
			bool firstCall = false;
			if (_currentLoadRomArgs == null)
			{
				firstCall = true;
				_currentLoadRomArgs = args;
			}
			else
			{
				args = _currentLoadRomArgs;
			}

			try
			{
				// movies should require deterministic emulation in ALL cases
				// if the core is managing its own DE through SyncSettings a 'deterministic' bool can be passed into the core's constructor
				// it is then up to the core itself to override its own local DeterministicEmulation setting
				bool deterministic = args.Deterministic ?? MovieSession.NewMovieQueued;

				if (!Tools.AskSave())
				{
					failureIsFromAskSave = true;
					return false;
				}

				var loader = new RomLoader(Config)
				{
					ChooseArchive = LoadArchiveChooser,
					ChoosePlatform = ChoosePlatformForRom,
					Deterministic = deterministic,
					MessageCallback = AddOnScreenMessage,
					OpenAdvanced = args.OpenAdvanced
				};
				FirmwareManager.RecentlyServed.Clear();

				loader.OnLoadError += ShowLoadError;
				loader.OnLoadSettings += CoreSettings;
				loader.OnLoadSyncSettings += CoreSyncSettings;

				// this also happens in CloseGame(). But it needs to happen here since if we're restarting with the same core,
				// any settings changes that we made need to make it back to config before we try to instantiate that core with
				// the new settings objects
				CommitCoreSettingsToConfig(); // adelikat: I Think by reordering things, this isn't necessary anymore
				CloseGame();

				var nextComm = CreateCoreComm();

				IOpenAdvanced ioa = args.OpenAdvanced;
				var oaOpenrom = ioa as OpenAdvanced_OpenRom;
				var ioaRetro = ioa as IOpenAdvancedLibretro;

				// we need to inform LoadRom which Libretro core to use...
				if (ioaRetro != null)
				{
					// prepare a core specification
					// if it wasn't already specified, use the current default
					if (ioaRetro.CorePath == null)
					{
						ioaRetro.CorePath = Config.LibretroCore;
					}

					if (ioaRetro.CorePath == null)
					{
						throw new InvalidOperationException("Can't load a file via Libretro until a core is specified");
					}
				}

				DisplayManager.ActivateOpenGLContext(); // required in case the core wants to create a shared OpenGL context

				bool result = string.IsNullOrEmpty(MovieSession.QueuedCoreName)
					? loader.LoadRom(path, nextComm, ioaRetro?.CorePath)
					: loader.LoadRom(path, nextComm, ioaRetro?.CorePath, forcedCoreName: MovieSession.QueuedCoreName);

				// we need to replace the path in the OpenAdvanced with the canonical one the user chose.
				// It can't be done until loader.LoadRom happens (for CanonicalFullPath)
				// i'm not sure this needs to be more abstractly engineered yet until we have more OpenAdvanced examples
				if (ioa is OpenAdvanced_Libretro oaRetro)
				{
					oaRetro.token.Path = loader.CanonicalFullPath;
				}

				if (oaOpenrom != null)
				{
					oaOpenrom.Path = loader.CanonicalFullPath;
				}

				if (result)
				{
					string openAdvancedArgs = $"*{OpenAdvancedSerializer.Serialize(ioa)}";
					Emulator.Dispose();
					Emulator = loader.LoadedEmulator;
					Game = loader.Game;
					Config.RecentCores.Enqueue(Emulator.Attributes().CoreName);
					while (Config.RecentCores.Count > 5) Config.RecentCores.Dequeue();
					InputManager.SyncControls(Emulator, MovieSession, Config);
					_multiDiskMode = false;

					// if (oaOpenrom is not null && ".xml".EqualsIgnoreCase(Path.GetExtension(oaOpenrom.Path.Replace("|", "")))
					// 	&& Emulator is not LibsnesCore)
					// {
					// 	// this is a multi-disk bundler file
					// 	// determine the xml assets and create RomStatusDetails for all of them
					// 	var xmlGame = XmlGame.Create(new HawkFile(oaOpenrom.Path));
					//
					// 	using var xSw = new StringWriter();
					//
					// 	for (int xg = 0; xg < xmlGame.Assets.Count; xg++)
					// 	{
					// 		var ext = Path.GetExtension(xmlGame.AssetFullPaths[xg])?.ToLowerInvariant();
					//
					// 		var (filename, data) = xmlGame.Assets[xg];
					// 		if (Disc.IsValidExtension(ext))
					// 		{
					// 			xSw.WriteLine(Path.GetFileNameWithoutExtension(filename));
					// 			xSw.WriteLine("SHA1:N/A");
					// 			xSw.WriteLine("MD5:N/A");
					// 			xSw.WriteLine();
					// 		}
					// 		else
					// 		{
					// 			xSw.WriteLine(filename);
					// 			xSw.WriteLine(SHA1Checksum.ComputePrefixedHex(data));
					// 			xSw.WriteLine(MD5Checksum.ComputePrefixedHex(data));
					// 			xSw.WriteLine();
					// 		}
					// 	}
					//
					// 	_defaultRomDetails = xSw.ToString();
					// 	_multiDiskMode = true;
					// }

					if (loader.LoadedEmulator is NES nes)
					{
						if (!string.IsNullOrWhiteSpace(nes.GameName))
						{
							Game.Name = nes.GameName;
						}

						Game.Status = nes.RomStatus;
					}
					// else if (loader.LoadedEmulator is QuickNES qns)
					// {
					// 	if (!string.IsNullOrWhiteSpace(qns.BootGodName))
					// 	{
					// 		Game.Name = qns.BootGodName;
					// 	}
					//
					// 	if (qns.BootGodStatus.HasValue)
					// 	{
					// 		Game.Status = qns.BootGodStatus.Value;
					// 	}
					// }

					var romDetails = Emulator.RomDetails();
					if (string.IsNullOrWhiteSpace(romDetails) && loader.Rom != null)
					{
						_defaultRomDetails = $"{Game.Name}\r\n{SHA1Checksum.ComputePrefixedHex(loader.Rom.RomData)}\r\n{MD5Checksum.ComputePrefixedHex(loader.Rom.RomData)}\r\n";
					}
					else if (string.IsNullOrWhiteSpace(romDetails) && loader.Rom == null)
					{
						// single disc game
						_defaultRomDetails = $"{Game.Name}\r\nSHA1:N/A\r\nMD5:N/A\r\n";
					}

					if (Emulator.HasBoardInfo())
					{
						Console.WriteLine("Core reported BoardID: \"{0}\"", Emulator.AsBoardInfo().BoardName);
					}

					Config.RecentRoms.Add(openAdvancedArgs);
					JumpLists.AddRecentItem(openAdvancedArgs, ioa.DisplayName);

					// Don't load Save Ram if a movie is being loaded
					if (!MovieSession.NewMovieQueued)
					{
						if (File.Exists(Config.PathEntries.SaveRamAbsolutePath(loader.Game, MovieSession.Movie)))
						{
							LoadSaveRam();
						}
						else if (Config.AutosaveSaveRAM && File.Exists(Config.PathEntries.SaveRamAbsolutePath(loader.Game, MovieSession.Movie)))
						{
							AddOnScreenMessage("AutoSaveRAM found, but SaveRAM was not saved");
						}
					}

					var previousRom = CurrentlyOpenRom;
					CurrentlyOpenRom = oaOpenrom?.Path ?? openAdvancedArgs;
					CurrentlyOpenRomArgs = args;

					Tools.Restart(Config, Emulator, Game);

					if (previousRom != CurrentlyOpenRom)
					{
						CheatList.NewList(Tools.GenerateDefaultCheatFilename(), autosave: true);
						if (Config.Cheats.LoadFileByGame && Emulator.HasMemoryDomains())
						{
							if (CheatList.AttemptToLoadCheatFile(Emulator.AsMemoryDomains()))
							{
								AddOnScreenMessage("Cheats file loaded");
							}
						}
					}
					else
					{
						if (Emulator.HasMemoryDomains())
						{
							CheatList.UpdateDomains(Emulator.AsMemoryDomains());
						}
						else
						{
							CheatList.NewList(Tools.GenerateDefaultCheatFilename(), autosave: true);
						}
					}

					OnRomChanged();
					DisplayManager.UpdateGlobals(Config, Emulator);
					DisplayManager.Blank();
					CreateRewinder();

					RewireSound();
					Tools.UpdateCheatRelatedTools(null, null);
					if (!MovieSession.NewMovieQueued && Config.AutoLoadLastSaveSlot && HasSlot(Config.SaveSlot))
					{
						_ = LoadstateCurrentSlot();
					}

					if (FirmwareManager.RecentlyServed.Count > 0)
					{
						Console.WriteLine("Active firmware:");
						foreach (var f in FirmwareManager.RecentlyServed)
						{
							Console.WriteLine($"\t{f.ID} : {f.Hash}");
						}
					}

					ExtToolManager.BuildToolStrip();

					RomLoaded?.Invoke(this, EventArgs.Empty);
					return true;
				}
				else if (Emulator.IsNull())
				{
					// This shows up if there's a problem
					Tools.Restart(Config, Emulator, Game);
					DisplayManager.UpdateGlobals(Config, Emulator);
					DisplayManager.Blank();
					ExtToolManager.BuildToolStrip();
					CheatList.NewList("", autosave: true);
					OnRomChanged();
					return false;
				}
				else
				{
					// The ROM has been loaded by a recursive invocation of the LoadROM method.
					RomLoaded?.Invoke(this, EventArgs.Empty);
					return true;
				}
			}
			finally
			{
				if (firstCall)
				{
					_currentLoadRomArgs = null;
				}

				_isLoadingRom = false;
			}
		}
		
		*/
}