﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Steamworks.Data;

namespace Steamworks
{
	public static class SteamClient
	{
		static bool initialized;

		/// <summary>
		/// Initialize the steam client.
		/// If <paramref name="asyncCallbacks"/> is false you need to call <see cref="RunCallbacks"/> manually every frame.
		/// </summary>
		public static void Init( uint appid, bool asyncCallbacks = true )
		{
			if ( initialized )
				throw new System.Exception( "Calling SteamClient.Init but is already initialized" );

			System.Environment.SetEnvironmentVariable( "SteamAppId", appid.ToString() );
			System.Environment.SetEnvironmentVariable( "SteamGameId", appid.ToString() );

			var interfaceVersions = Helpers.BuildVersionString(
				ISteamApps.Version,
				ISteamFriends.Version,
				ISteamInput.Version,
				ISteamInventory.Version,
				ISteamMatchmaking.Version,
				ISteamMatchmakingServers.Version,
				ISteamMusic.Version,
				ISteamNetworking.Version,
				ISteamNetworkingSockets.Version,
				ISteamNetworkingUtils.Version,
				ISteamParentalSettings.Version,
				ISteamParties.Version,
				ISteamRemoteStorage.Version,
				ISteamScreenshots.Version,
				ISteamUGC.Version,
				ISteamUser.Version,
				ISteamUserStats.Version,
				ISteamUtils.Version,
				ISteamVideo.Version,
				ISteamRemotePlay.Version,
				ISteamTimeline.Version );
			var result = SteamAPI.Init( interfaceVersions, out var error );
			if ( result != SteamAPIInitResult.OK )
			{
				throw new System.Exception( $"SteamApi_Init failed with {result} - error: {error}" );
			}

			AppId = appid;

			initialized = true;

			//
			// Dispatch is responsible for pumping the event loop.
			//
			Dispatch.Init();
			Dispatch.ClientPipe = SteamAPI.GetHSteamPipe();

			// Note: don't forget to add the interface version to SteamAPI.Init above!!!
			AddInterface<SteamApps>();
			AddInterface<SteamFriends>();
			AddInterface<SteamInput>();
			AddInterface<SteamInventory>();
			AddInterface<SteamMatchmaking>();
			AddInterface<SteamMatchmakingServers>();
			AddInterface<SteamMusic>();
			AddInterface<SteamNetworking>();
			AddInterface<SteamNetworkingSockets>();
			AddInterface<SteamNetworkingUtils>();
			AddInterface<SteamParental>();
			AddInterface<SteamParties>();
			AddInterface<SteamRemoteStorage>();
			AddInterface<SteamScreenshots>();
			AddInterface<SteamUGC>();
			AddInterface<SteamUser>();
			AddInterface<SteamUserStats>();
			AddInterface<SteamUtils>();
			AddInterface<SteamVideo>();
			AddInterface<SteamRemotePlay>();
			AddInterface<SteamTimeline>();
			// Note: don't forget to add the interface version to SteamAPI.Init above!!!

			initialized = openInterfaces.Count > 0;

			if ( asyncCallbacks )
			{
				//
				// This will keep looping in the background every 16 ms
				// until we shut down.
				//
				Dispatch.LoopClientAsync();
			}
		}

		internal static void AddInterface<T>() where T : SteamClass, new()
		{
			var t = new T();
			bool valid = t.InitializeInterface( false );
			if ( valid )
			{
				openInterfaces.Add( t );
			}
			else
			{
				t.DestroyInterface( false );
			}
		}

		static readonly List<SteamClass> openInterfaces = new List<SteamClass>();

		internal static void ShutdownInterfaces()
		{
			foreach ( var e in openInterfaces )
			{
				e.DestroyInterface( false );
			}

			openInterfaces.Clear();
		}

		public static bool IsValid => initialized;

		/// <summary>
		/// Shuts down the steam client.
		/// </summary>
		public static void Shutdown()
		{
			if ( !IsValid ) return;

			Cleanup();

			SteamAPI.Shutdown();
		}

		internal static void Cleanup()
		{
			Dispatch.ShutdownClient();

			initialized = false;
			ShutdownInterfaces();
		}

		public static void RunCallbacks()
		{
			if ( Dispatch.ClientPipe != 0 )
				Dispatch.Frame( Dispatch.ClientPipe );
		}

		/// <summary>
		/// Checks if the current user's Steam client is connected to the Steam servers.
		/// <para>
		/// If it's not, no real-time services provided by the Steamworks API will be enabled. The Steam 
		/// client will automatically be trying to recreate the connection as often as possible. When the 
		/// connection is restored a SteamServersConnected_t callback will be posted.
		/// You usually don't need to check for this yourself. All of the API calls that rely on this will 
		/// check internally. Forcefully disabling stuff when the player loses access is usually not a 
		/// very good experience for the player and you could be preventing them from accessing APIs that do not 
		/// need a live connection to Steam.
		/// </para>
		/// </summary>
		public static bool IsLoggedOn => SteamUser.Internal != null && SteamUser.Internal.BLoggedOn();

		/// <summary>
		/// Gets the Steam ID of the account currently logged into the Steam client. This is 
		/// commonly called the 'current user', or 'local user'.
		/// A Steam ID is a unique identifier for a Steam accounts, Steam groups, Lobbies and Chat 
		/// rooms, and used to differentiate users in all parts of the Steamworks API.
		/// </summary>
		public static SteamId SteamId => SteamUser.Internal?.GetSteamID() ?? default;

		/// <summary>
		/// returns the local players name - guaranteed to not be <see langword="null"/>.
		/// This is the same name as on the user's community profile page.
		/// </summary>
		public static string? Name => SteamFriends.Internal?.GetPersonaName();

		/// <summary>
		/// Gets the status of the current user.
		/// </summary>
		public static FriendState State => SteamFriends.Internal?.GetPersonaState() ?? FriendState.Offline;

		/// <summary>
		/// Returns the App ID of the current process.
		/// </summary>
		public static AppId AppId { get; internal set; }

		/// <summary>
		/// Checks if your executable was launched through Steam and relaunches it through Steam if it wasn't.
		/// <para>
		///  This returns true then it starts the Steam client if required and launches your game again through it, 
		///  and you should quit your process as soon as possible. This effectively runs steam://run/AppId so it 
		///  may not relaunch the exact executable that called it, as it will always relaunch from the version 
		///  installed in your Steam library folder/
		///  Note that during development, when not launching via Steam, this might always return true.
		///  </para>
		/// </summary>
		public static bool RestartAppIfNecessary( uint appid )
		{
			// Having these here would probably mean it always returns false?

			//System.Environment.SetEnvironmentVariable( "SteamAppId", appid.ToString() );
			//System.Environment.SetEnvironmentVariable( "SteamGameId", appid.ToString() );

			return SteamAPI.RestartAppIfNecessary( appid );
		}

		/// <summary>
		/// Called in interfaces that rely on this being initialized
		/// </summary>
		internal static void ValidCheck()
		{
			if ( !IsValid )
				throw new System.Exception( "SteamClient isn't initialized" );
		}

	}
}
