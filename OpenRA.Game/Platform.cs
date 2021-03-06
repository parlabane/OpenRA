#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenRA
{
	public enum PlatformType { Unknown, Windows, OSX, Linux }

	public static class Platform
	{
		public static PlatformType CurrentPlatform { get { return currentPlatform.Value; } }
		public static readonly Guid SessionGUID = Guid.NewGuid();

		static Lazy<PlatformType> currentPlatform = Exts.Lazy(GetCurrentPlatform);

		static PlatformType GetCurrentPlatform()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				return PlatformType.Windows;

			try
			{
				var psi = new ProcessStartInfo("uname", "-s");
				psi.UseShellExecute = false;
				psi.RedirectStandardOutput = true;
				var p = Process.Start(psi);
				var kernelName = p.StandardOutput.ReadToEnd();
				if (kernelName.Contains("Darwin"))
					return PlatformType.OSX;

				return PlatformType.Linux;
			}
			catch { }

			return PlatformType.Unknown;
		}

		public static string RuntimeVersion
		{
			get
			{
				var mono = Type.GetType("Mono.Runtime");
				if (mono == null)
					return ".NET CLR {0}".F(Environment.Version);

				var version = mono.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
				if (version == null)
					return "Mono (unknown version) CLR {0}".F(Environment.Version);

				return "Mono {0} CLR {1}".F(version.Invoke(null, null), Environment.Version);
			}
		}

		/// <summary>
		/// Directory containing user-specific support files (settings, maps, replays, game data, etc).
		/// The directory will automatically be created if it does not exist when this is queried.
		/// </summary>
		public static string SupportDir { get { return supportDir.Value; } }
		static Lazy<string> supportDir = Exts.Lazy(GetSupportDir);

		static string GetSupportDir()
		{
			// Use a local directory in the game root if it exists (shared with the system support dir)
			var localSupportDir = Path.Combine(GameDir, "Support");
			if (Directory.Exists(localSupportDir))
				return localSupportDir + Path.DirectorySeparatorChar;

			var dir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

			switch (CurrentPlatform)
			{
				case PlatformType.Windows:
					dir += Path.DirectorySeparatorChar + "OpenRA";
					break;
				case PlatformType.OSX:
					dir += "/Library/Application Support/OpenRA";
					break;
				default:
					dir += "/.openra";
					break;
			}

			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			return dir + Path.DirectorySeparatorChar;
		}

		/// <summary>
		/// Directory containing system-wide support files (mod metadata).
		/// This directory is not guaranteed to exist or be writable.
		/// Consumers are expected to check the validity of the returned value, and
		/// fall back to the user support directory if necessary.
		/// </summary>
		public static string SystemSupportDir { get { return systemSupportDir.Value; } }
		static Lazy<string> systemSupportDir = Exts.Lazy(GetSystemSupportDir);

		static string GetSystemSupportDir()
		{
			// Use a local directory in the game root if it exists (shared with the system support dir)
			var localSupportDir = Path.Combine(GameDir, "Support");
			if (Directory.Exists(localSupportDir))
				return localSupportDir + Path.DirectorySeparatorChar;

			switch (CurrentPlatform)
			{
				case PlatformType.Windows:
					return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenRA") + Path.DirectorySeparatorChar;
				case PlatformType.OSX:
					return "/Library/Application Support/OpenRA/";
				default:
					return "/var/games/openra/";
			}
		}

		public static string GameDir
		{
			get
			{
				var dir = AppDomain.CurrentDomain.BaseDirectory;

				// Add trailing DirectorySeparator for some buggy AppPool hosts
				if (!dir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
					dir += Path.DirectorySeparatorChar;

				return dir;
			}
		}

		/// <summary>Replaces special character prefixes with full paths.</summary>
		public static string ResolvePath(string path)
		{
			path = path.TrimEnd(' ', '\t');

			// Paths starting with ^ are relative to the support dir
			if (path.StartsWith("^", StringComparison.Ordinal))
				path = SupportDir + path.Substring(1);

			// Paths starting with . are relative to the game dir
			if (path == ".")
				return GameDir;

			if (path.StartsWith("./", StringComparison.Ordinal) || path.StartsWith(".\\", StringComparison.Ordinal))
				path = GameDir + path.Substring(2);

			return path;
		}

		/// <summary>Replace special character prefixes with full paths.</summary>
		public static string ResolvePath(params string[] path)
		{
			return ResolvePath(path.Aggregate(Path.Combine));
		}

		/// <summary>Replace the full path prefix with the special notation characters ^ or .</summary>
		public static string UnresolvePath(string path)
		{
			if (path.StartsWith(SupportDir, StringComparison.Ordinal))
				path = "^" + path.Substring(SupportDir.Length);

			if (path.StartsWith(GameDir, StringComparison.Ordinal))
				path = "./" + path.Substring(GameDir.Length);

			return path;
		}
	}
}
