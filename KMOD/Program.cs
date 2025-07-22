using fastJSON5;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Hideout;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace KMOD;

public record ModMetadata : AbstractModMetadata
{
	public override string? ModGuid { get; set; } = "74be107d-80b0-47e5-8b4c-84d2e4ff5850";
	public override string? Name { get; set; } = "KMOD";
	public override string? Author { get; set; } = "Krinkels";
	public override List<string>? Contributors { get; set; } = new() { "", "" };
	public override string? Version { get; set; } = "1.4.0";
	public override string? SptVersion { get; set; } = "4.0.0";
	public override List<string>? LoadBefore { get; set; } = null;
	public override List<string>? LoadAfter { get; set; } = null;
	public override List<string>? Incompatibilities { get; set; } = null;
	public override Dictionary<string, string>? ModDependencies { get; set; } = null;
	public override string? Url { get; set; } = "https://github.com/Krinkelss/kmod_sharp";
	public override bool? IsBundleMod { get; set; } = false;
	public override string? Licence { get; set; } = "MIT";
}

[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
public class KMOD(
	ISptLogger<KMOD> logger,
	//HashUtil hashUtil,
	DatabaseService databaseService,
	//TraderController traderController,
	ModHelper modHelper,
	ConfigServer _configServer
) : IOnLoad
{
	//_props - Properties
	public Task OnLoad()
	{
		Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();
		var hideout = databaseService.GetHideout();
		TraderConfig traderConfig = _configServer.GetConfig<TraderConfig>();
		SPTarkov.Server.Core.Models.Eft.Common.Config globals = databaseService.GetGlobals().Configuration;


		// ******************************************************************************
		// Загружаем наши настройки
		var pathToMod = modHelper.GetAbsolutePathToModFolder( Assembly.GetExecutingAssembly() );
		
		KConfig Config = null;
		try
		{
			Config = JSON5.ToObject<KConfig>( modHelper.GetRawFileData( pathToMod, "Config.json5" ) );
		}
		catch( Exception e )
		{
			logger.Error( $"[KMOD] Ошибка при загрузке Config.json5: {e.Message}" );			
			return Task.CompletedTask;
		}

		if( Config.Items?.Enable == true )
		{
			foreach( var id in items.Keys )
			{
				var baseItem = items[ id ];

				// Убрать лимит ключей
				if( Config.Items?.RemoveKeysUsageNumber == true && ( ( baseItem.Parent == BaseClasses.KEY_MECHANICAL || baseItem.Parent == BaseClasses.KEYCARD ) && baseItem.Properties.MaximumNumberOfUsage != null ) )
				{
					// Одноразовые ключи используются только единожды
					if( Config.Items?.AvoidSingleKeys == true && baseItem.Properties.MaximumNumberOfUsage == 1 )
						continue;

					baseItem.Properties.MaximumNumberOfUsage = 0;
				}
								
				// Насколько увеличить число патронов в ячейке
				if( Config.Items?.StackMaxSize > 0 && baseItem.Parent == BaseClasses.AMMO && baseItem.Properties.StackMaxSize != null )
				{
					baseItem.Properties.StackMaxSize += Config.Items?.StackMaxSize;
				}				
			}

			// Множитель времени постройки в убежище. Меньше - быстрее. При -1 мгновенная постройка	
			foreach( var area in hideout.Areas )
			{
				foreach( var stage in area.Stages )
				{
					if( stage.Value.ConstructionTime > 0 && Config.Items?.HideoutConstMult != -1 )
					{
						stage.Value.ConstructionTime = Math.Max( 5, ( int )( stage.Value.ConstructionTime * Config.Items?.HideoutConstMult ) );
					}
					else
					{
						stage.Value.ConstructionTime = 0;
					}
				}
			}

			// Множитель времени производства
			foreach( var area in hideout.Production.Recipes )
			{
				if( area.Continuous == false && area.ProductionTime >= 1 )
				{
					if( Config.Items?.HideoutProdMult != -1 )
					{
						area.ProductionTime = Math.Max( 5, ( int )( area.ProductionTime * Config.Items?.HideoutProdMult ) );
					}
					else
					{
						area.ProductionTime = 10;
					}
				}			
			}

			// Возможность продавать биткоин на барахолке
			if( Config.Items?.BitcoinSellOnRagfair == true )
			{
				items[ ItemTpl.BARTER_PHYSICAL_BITCOIN ].Properties.CanSellOnRagfair = true;
			}

			// Время применения Surv12. Указывать точное время в секундах
			if( Config.Items?.Surv12UseTime > 0 )
			{
				items[ ItemTpl.MEDICAL_SURV12_FIELD_SURGICAL_KIT ].Properties.MedUseTime = Config.Items?.Surv12UseTime;
			}

			// У всех торговцев товар "найден в рейде"
			if( Config.Items?.TraderPurchasesFoundInRaid == true )
			{
				traderConfig.PurchasesAreFoundInRaid = true;
			}

			// Изменить размер защищённого контейнера
			if( Config.Items?.SecureContainers?.Enable == true )
			{
				items[ Config.Items?.SecureContainers?.Secure_Container_Name ].Properties.Grids[ 0 ].Props.CellsH = Config.Items?.SecureContainers?.HSize;
				items[ Config.Items?.SecureContainers?.Secure_Container_Name ].Properties.Grids[ 0 ].Props.CellsV = Config.Items?.SecureContainers?.VSize;
			}

			// logger.LogWithColor( $"1 = {Config.Items?.SecureContainers?.Secure_Container_Name}", LogTextColor.Cyan );
		}

		if( Config.Weapons?.Enable == true )
		{
			// Ремонт не изнашивает броню
			if( Config.Weapons?.OpArmorRepair == true )
			{
				foreach( var armormats in globals.ArmorMaterials.Values )
				{
					armormats.MaxRepairDegradation = 0;
					armormats.MinRepairDegradation = 0;
					armormats.MaxRepairKitDegradation = 0;
					armormats.MinRepairKitDegradation = 0;
				}
			}

			if( Config.Weapons?.OpGunRepair == true )
			{
				// Ремонт не изнашивает оружие
				foreach( var item in items.Values )
				{
					if( item.Properties?.MaxRepairDegradation != null && item.Properties.MaxRepairKitDegradation != null )
					{
						item.Properties.MinRepairDegradation = 0;
						item.Properties.MaxRepairDegradation = 0;
						item.Properties.MinRepairKitDegradation = 0;
						item.Properties.MaxRepairKitDegradation = 0;
					}
				}
			}

			foreach( var id in items.Keys )
			{
				var baseItem = items[ id ];

				// Без перегрева оружия
				if( Config.Weapons?.WeaponHeatOff == true && baseItem.Properties.AllowOverheat != null )
				{
					baseItem.Properties.AllowOverheat = false;
				}

				// Процент уменьшения/увеличения времени зарядки магазина
				if( Config.Weapons?.LoadUnloadModifier > 0 && baseItem.Parent == BaseClasses.MAGAZINE && baseItem.Properties?.LoadUnloadModifier != null )
				{
					baseItem.Properties.LoadUnloadModifier = Config.Weapons?.LoadUnloadModifier;
				}

			}
		}


		//logger.Info( "	------------------------------End" );

		return Task.CompletedTask;
	}
}

[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
public class KTRADER_SIDR(
	KTRADER TRADER
) : IOnLoad
{	
	public Task OnLoad()
	{
		TRADER.SIDR_Enable();
		return Task.CompletedTask;
	}
}

[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
public class KTRADER_MERCHANT(
	KTRADER TRADER
) : IOnLoad
{
	public Task OnLoad()
	{
		TRADER.MERCHANT_Enable();
		return Task.CompletedTask;
	}
}

// Свой собственный профиль
[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
public class MY_PROFILE(
	ModHelper modHelper,
	DatabaseService databaseService
) : IOnLoad
{
	public Task OnLoad()
	{
		Dictionary<string, ProfileSides> Profile = databaseService.GetProfileTemplates();

		var pathToMod = modHelper.GetAbsolutePathToModFolder( Assembly.GetExecutingAssembly() );
		var myProfile = modHelper.GetJsonDataFromFile<ProfileSides>( pathToMod, "Profile/Unheard.json" );

		Profile.TryAdd( "Krinkels Unheard Profile", myProfile );

		return Task.CompletedTask;
	}
}

// Для квеста Механика "Оружейник"
[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
public class WPMOD(
	ISptLogger<WPMOD> logger,
	ModHelper modHelper,
	FileUtil fileUtil,
	JsonUtil jsonUtil,
	ConfigServer _configServer
	) : IOnLoad
{
	public Task OnLoad()
	{
		GiftsConfig akigifts = _configServer.GetConfig<GiftsConfig>();
		var pathToMod = modHelper.GetAbsolutePathToModFolder( Assembly.GetExecutingAssembly() );

		var wpMod = new Gift
		{
			Sender = GiftSenderType.System,
			MessageText = "Лень оружие для механика собирать? Эх ты, ладно, вот тебе сборки",
			CollectionTimeHours = 48,
			AssociatedEvent = SeasonalEventType.None,
			MaxToSendPlayer = 1,
			Items = []
		};

		var wpFolder = System.IO.Path.Combine( pathToMod, "wpmod" );
		string[] wpFiles = Directory.GetFiles( wpFolder, "*.json", SearchOption.TopDirectoryOnly );

		foreach( string file in wpFiles )
		{
			string json = fileUtil.ReadFile( file );
			WeaponBuildChange jsData = jsonUtil.Deserialize<WeaponBuildChange>( json );

			wpMod.Items.AddRange( jsData.Items );
		}

		//logger.LogWithColor( $"############## {outputJson}", LogTextColor.Cyan );

		akigifts.Gifts.TryAdd( "TapokWpMod", wpMod );

		return Task.CompletedTask;
	}
}

/**/

/*
[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
public record GIFT(
	ISptLogger<GIFT> logger,
	KGIFT KGIFT // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
) : IOnLoad
{
	public Task OnLoad()
	{
		KGIFT.KGIFT_Enable();
		return Task.CompletedTask;
	}
}*/