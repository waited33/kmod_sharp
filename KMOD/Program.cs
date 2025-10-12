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
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace KMOD;

public record ModMetadata : AbstractModMetadata
{
	public override string? ModGuid { get; init; } = "74be107d-80b0-47e5-8b4c-84d2e4ff5850";
	public override string? Name { get; init; } = "KMOD";
	public override string? Author { get; init; } = "Krinkels";
	public override List<string>? Contributors { get; init; }
	public override SemanticVersioning.Version Version { get; init; } = new( "1.4.0" );
	public override SemanticVersioning.Range SptVersion { get; init; } = new( "4.0.0" );
	public override List<string>? Incompatibilities { get; init; }
	public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
	public override string? Url { get; init; } = "https://github.com/Krinkelss/kmod_sharp";
	public override bool? IsBundleMod { get; init; } = false;
	public override string? License { get; init; } = "MIT";
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
		BotConfig botConfig = _configServer.GetConfig<BotConfig>();
		

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

		// Настройка предметов
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

			// Ремонт не изнашивает броню
			if( Config.Items?.OpArmorRepair == true )
			{
				foreach( var armormats in globals.ArmorMaterials.Values )
				{
					armormats.MaxRepairDegradation = 0;
					armormats.MinRepairDegradation = 0;
					armormats.MaxRepairKitDegradation = 0;
					armormats.MinRepairKitDegradation = 0;
				}
			}

			// Ремонт не изнашивает оружие
			if( Config.Items?.OpGunRepair == true )
			{
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

			// Изменить размер защищённого контейнера
			/*if( Config.Items?.SecureContainers?.Enable == true )
			{
				items[ Config.Items?.SecureContainers?.Secure_Container_Name ].Properties.Grids[ 0 ].Props.CellsH = Config.Items?.SecureContainers?.HSize;
				items[ Config.Items?.SecureContainers?.Secure_Container_Name ].Properties.Grids[ 0 ].Props.CellsV = Config.Items?.SecureContainers?.VSize;
			}*/

			// logger.LogWithColor( $"1 = {Config.Items?.SecureContainers?.Secure_Container_Name}", LogTextColor.Cyan );
		}

		// Настройка оружия
		if( Config.Weapons?.Enable == true )
		{
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

		// Настройки пользователя
		if( Config.Player?.Enable == true )
		{
			// Бесконечная выносливость
			if( Config.Player?.UnlimitedStamina == true )
			{
				globals.Stamina.Capacity = 500;
				globals.Stamina.BaseRestorationRate = 500;
				globals.Stamina.StaminaExhaustionCausesJiggle = false;
				globals.Stamina.StaminaExhaustionStartsBreathSound = false;
				globals.Stamina.StaminaExhaustionRocksCamera = false;
				globals.Stamina.SprintDrainRate = 0;
				globals.Stamina.JumpConsumption = 0;
				globals.Stamina.AimDrainRate = 0;
				globals.Stamina.SitToStandConsumption = 0;
			}

			// Множитель опыта навыкам
			globals.SkillsSettings.SkillProgressRate = Config.Player?.SkillProgMult ?? 0.4;

			// Множитель прокачки оружия
			globals.SkillsSettings.WeaponSkillProgressRate = Config.Player?.WeaponSkillMult ?? 1;

			//--------------------------
			// Сколько длится усталость
			globals.SkillFatigueReset = Config.Player?.Skills?.SkillFatigueReset ?? 200;

			// Очки бодрости
			globals.SkillFreshPoints = Config.Player?.Skills?.SkillFreshPoints ?? 1;

			// % эффективности "свежих" навыков
			globals.SkillFreshEffectiveness = Config.Player?.Skills?.SkillFreshEffectiveness ?? 1.3;

			// Усталость за очко
			globals.SkillFatiguePerPoint = Config.Player?.Skills?.SkillFatiguePerPoint ?? 0.6;

			// Опыт при максимальной усталости
			globals.SkillMinEffectiveness = Config.Player?.Skills?.SkillMinEffect ?? 0.0001;

			// Очки перед наступлением усталости
			globals.SkillPointsBeforeFatigue = Config.Player?.Skills?.SkillPointsBeforeFatigue ?? 1;
		}

		if( Config.Raids?.Enable == true )
		{
			var mapsDb = databaseService.GetLocations();
			var mapsDict = mapsDb.GetDictionary();

			// Выход с любой стороны
			if( Config.Raids.ExtendedExtracts == true )
			{
				Dictionary<string, string> entryPointsMap = new Dictionary<string, string>
				{
					{ "bigmap", "Customs,Boiler Tanks" },
					{ "interchange", "MallSE,MallNW" },
					{ "shoreline", "Village,Riverside" },
					{ "woods", "House,Old Station" },
					{ "lighthouse", "Tunnel,North" },
					{ "tarkovstreets", "E1_2,E6_1,E2_3,E3_4,E4_5,E5_6,E6_1" },
					{ "sandbox", "west,east" },
					{ "sandbox_high", "west,east" }
				};

				foreach( var ( key, cap ) in botConfig.MaxBotCap )
				{					
					if( entryPointsMap.TryGetValue( key, out var entryPoints ) )
					{
						SPTarkov.Server.Core.Models.Eft.Common.Location? Map = databaseService.GetLocation( key );
						if( Map == null )
							continue;

						foreach( var exit in Map.Base.Exits )
						{
							exit.EntryPoints = entryPoints;

							// Выход на машине
							if( exit.PassageRequirement == SPTarkov.Server.Core.Models.Enums.RequirementState.TransferItem )
							{
								exit.ExfiltrationTime = Config.Raids.CarExtractTime;
							}
						}
					}
				}
			}

			// Выходы с шансом всегда доступны
			if( Config.Raids.ChanceExtracts == true )
			{
				foreach( var ( key, cap ) in botConfig.MaxBotCap )
				{
					if( !mapsDict.TryGetValue( mapsDb.GetMappedKey( key ), out var map ) )
					{
						continue;
					}

					SPTarkov.Server.Core.Models.Eft.Common.Location? Map = databaseService.GetLocation( key );
					if( Map == null )
						continue;
										
					foreach( var exit in Map.Base.Exits )
					{
						//if( exit.Name != "EXFIL_Train" )
						if( exit.PassageRequirement == SPTarkov.Server.Core.Models.Enums.RequirementState.Train )
						{
							exit.Chance = 100;
						}
					}
				}
			}

			// Разрешить совместные выходы в одного
			if( Config.Raids.FreeCoop == true )
			{
				foreach( var ( key, cap ) in botConfig.MaxBotCap )
				{
					if( !mapsDict.TryGetValue( mapsDb.GetMappedKey( key ), out var map ) )
					{
						continue;
					}

					SPTarkov.Server.Core.Models.Eft.Common.Location? Map = databaseService.GetLocation( key );
					if( Map == null )
						continue;
										
					foreach( var exit in Map.Base.Exits )
					{
						if( exit.PassageRequirement == SPTarkov.Server.Core.Models.Enums.RequirementState.ScavCooperation )
						{
							exit.PassageRequirement = SPTarkov.Server.Core.Models.Enums.RequirementState.None;
							exit.ExfiltrationType = SPTarkov.Server.Core.Models.Enums.ExfiltrationType.Individual;
							exit.Id = "";
							exit.Count = 0;
							exit.PlayersCount = 0;
							exit.PlayersCountPVE = 0;
							exit.RequirementTip = "";
							if( exit.RequiredSlot != null )
							{
								exit.RequiredSlot = null;
							}
						}
					}
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