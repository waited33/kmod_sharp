using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KMOD
{	
	[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
	public class KTRADER(
		ISptLogger<KTRADER> logger,
		ModHelper modHelper,
		ImageRouter imageRouter,
		ConfigServer configServer,
		TimeUtil timeUtil,
		ICloner cloner,
		DatabaseService databaseService,
		CustomItemService customitemservice, 
		LocaleService localeService )
	{
		private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
		private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();
		public void SIDR_Enable()
		{
			var pathToMod = modHelper.GetAbsolutePathToModFolder( Assembly.GetExecutingAssembly() );
			var traderImagePath = System.IO.Path.Combine( pathToMod, "Sidr/img/Sidr.jpg" );
			var traderBase = modHelper.GetJsonDataFromFile<TraderBase>( pathToMod, "Sidr/base.json" );

			imageRouter.AddRoute(traderBase.Avatar.Replace( ".jpg", "" ), traderImagePath );
			SetTraderUpdateTime(_traderConfig, traderBase, timeUtil.GetHoursAsSeconds( 1 ), timeUtil.GetHoursAsSeconds( 2 ) );

			// Add our trader to the config file, this lets it be seen by the flea market
			_ragfairConfig.Traders.TryAdd(traderBase.Id, true );

			// Add our trader (with no items yet) to the server database
			// An 'assort' is the term used to describe the offers a trader sells, it has 3 parts to an assort
			// 1: The item
			// 2: The barter scheme, cost of the item (money or barter)
			// 3: The Loyalty level, what rep level is required to buy the item from trader
			AddTraderWithEmptyAssortToDb(traderBase );

			// Add localisation text for our trader to the database so it shows to people playing in different languages
			AddTraderToLocales(traderBase, "Сидорович", "Прижимистый торговец, поселившийся на окраине таркова. Поговаривают что может достать практически всё" );

			// Get the assort data from JSON
			var assort = modHelper.GetJsonDataFromFile<TraderAssort>( pathToMod, "Sidr/assort.json" );

			// Save the data we loaded above into the trader we've made
			OverwriteTraderAssort(traderBase.Id, assort);

			RagfairConfig Ragfair = configServer.GetConfig<RagfairConfig>();
			Ragfair.Traders[ traderBase.Id ] = true;
		}

		public void MERCHANT_Enable()
		{
			var pathToMod = modHelper.GetAbsolutePathToModFolder( Assembly.GetExecutingAssembly() );
			var traderImagePath = System.IO.Path.Combine( pathToMod, "Merchant/img/Merchant.jpg" );
			var traderBase = modHelper.GetJsonDataFromFile<TraderBase>( pathToMod, "Merchant/base.json" );

			imageRouter.AddRoute( traderBase.Avatar.Replace( ".jpg", "" ), traderImagePath );
			SetTraderUpdateTime( _traderConfig, traderBase, timeUtil.GetHoursAsSeconds( 1 ), timeUtil.GetHoursAsSeconds( 2 ) );

			// Add our trader to the config file, this lets it be seen by the flea market
			_ragfairConfig.Traders.TryAdd( traderBase.Id, true );

			// Add our trader (with no items yet) to the server database
			// An 'assort' is the term used to describe the offers a trader sells, it has 3 parts to an assort
			// 1: The item
			// 2: The barter scheme, cost of the item (money or barter)
			// 3: The Loyalty level, what rep level is required to buy the item from trader
			AddTraderWithEmptyAssortToDb( traderBase );

			// Add localisation text for our trader to the database so it shows to people playing in different languages
			AddTraderToLocales( traderBase, "Торговец", "Загадочный персонаж, торгующий некоторыми товарами" );

			// Get the assort data from JSON
			var assort = modHelper.GetJsonDataFromFile<TraderAssort>( pathToMod, "Merchant/assort.json" );

			// Save the data we loaded above into the trader we've made
			OverwriteTraderAssort( traderBase.Id, assort );

			RagfairConfig Ragfair = configServer.GetConfig<RagfairConfig>();
			Ragfair.Traders[ traderBase.Id ] = true;
		}

		/// <summary>
		/// Add the traders update time for when their offers refresh
		/// </summary>
		/// <param name="traderConfig">trader config to add our trader to</param>
		/// <param name="baseJson">json file for trader (db/base.json)</param>
		/// <param name="refreshTimeSecondsMin">How many seconds between trader stock refresh min time</param>
		/// <param name="refreshTimeSecondsMax">How many seconds between trader stock refresh max time</param>
		private void SetTraderUpdateTime( TraderConfig traderConfig, TraderBase baseJson, int refreshTimeSecondsMin, int refreshTimeSecondsMax )
		{
			// Add refresh time in seconds to config
			var traderRefreshRecord = new UpdateTime
			{
				TraderId = baseJson.Id,
				Seconds = new MinMax<int>( refreshTimeSecondsMin, refreshTimeSecondsMax )
			};

			traderConfig.UpdateTime.Add( traderRefreshRecord );
		}

		/// <summary>
		/// Add a traders base data to the server, no assort items
		/// </summary>
		/// <param name="traderDetailsToAdd">trader details</param>
		public void AddTraderWithEmptyAssortToDb( TraderBase traderDetailsToAdd )
		{
			// Create an empty assort ready for our items
			var emptyTraderItemAssortObject = new TraderAssort
			{
				Items = [],
				BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
				LoyalLevelItems = new Dictionary<MongoId, int>()
			};

			// Create trader data ready to add to database
			var traderDataToAdd = new Trader
			{
				Assort = emptyTraderItemAssortObject,
				Base = cloner.Clone( traderDetailsToAdd ),
				QuestAssort = new Dictionary<string, Dictionary<string, string>> // quest assort is empty as trader has no assorts unlocked by quests
            {
                // We create 3 empty arrays, one for each of the main statuses that are possible
                { "Started", new Dictionary<string, string>() },
				{ "Success", new Dictionary<string, string>() },
				{ "Fail", new Dictionary<string, string>() }
			}
			};

			// Add the new trader id and data to the server
			if( !databaseService.GetTables().Traders.TryAdd( traderDetailsToAdd.Id, traderDataToAdd ) )
			{
				//Failed to add trader!
			}
		}

		/// <summary>
		/// Add traders name/location/description to all locales (e.g. German/French/English)
		/// </summary>
		/// <param name="baseJson">json file for trader (db/base.json)</param>
		/// <param name="firstName">First name of trader</param>
		/// <param name="description">Flavor text of whom the trader is</param>
		private void AddTraderToLocales( TraderBase baseJson, string firstName, string description )
		{
			// For each language, add locale for the new trader
			/*var locales = databaseService.GetTables().Locales.Global;
			var newTraderId = baseJson.Id;
			var fullName = baseJson.Name;
			var nickName = baseJson.Nickname;
			var location = baseJson.Location;

			foreach( var (localeKey, localeKvP) in locales )
			{
				localeService.AddCustomClientLocale( localeKey, $"{newTraderId} FullName", fullName );
				localeService.AddCustomClientLocale( localeKey, $"{newTraderId} FirstName", firstName );
				localeService.AddCustomClientLocale( localeKey, $"{newTraderId} Nickname", nickName );
				localeService.AddCustomClientLocale( localeKey, $"{newTraderId} Location", location );
				localeService.AddCustomClientLocale( localeKey, $"{newTraderId} Description", description );
			}*/

			var languages = databaseService.GetLocales().Languages;
			foreach( var shortNameKey in languages )
			{
				var newTraderId = baseJson.Id;
				var fullName = baseJson.Name;
				var nickName = baseJson.Nickname;
				var location = baseJson.Location;

				if( databaseService.GetLocales().Global.TryGetValue( shortNameKey.Key, out var lazyLoad ) )
				{
					lazyLoad.AddTransformer( localeData =>
					{
						localeData?.Add( $"{newTraderId} FullName", fullName ?? "" );
						localeData?.Add( $"{newTraderId} FirstName", firstName ?? "" );
						localeData?.Add( $"{newTraderId} Nickname", nickName ?? "" );
						localeData?.Add( $"{newTraderId} Location", location ?? "" );
						localeData?.Add( $"{newTraderId} Description", description ?? "" );

						return localeData;
					} );
				}
			}
		}

		/// <summary>
		/// Overwrite the desired traders assorts with the ones provided
		/// </summary>
		/// <param name="traderId">Trader to override assorts of</param>
		/// <param name="newAssorts">new assorts we want to add</param>
		private void OverwriteTraderAssort( string traderId, TraderAssort newAssorts )
		{
			if( !databaseService.GetTables().Traders.TryGetValue( traderId, out var traderToEdit ) )
			{
				logger.Warning( $"Unable to update assorts for trader: {traderId}, they couldn't be found on the server" );

				return;
			}

			// Override the traders assorts with the ones we passed in
			traderToEdit.Assort = newAssorts;
		}
	}
}
