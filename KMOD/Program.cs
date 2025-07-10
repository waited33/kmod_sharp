// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");

using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models;
using SPTarkov.Server.Core.Models.External;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Enums;
using System.Diagnostics;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Server;
using System.Threading;
using System.Reflection;
using System.Text.Json;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server;
using System.Runtime.InteropServices.JavaScript;
using SPTarkov.Server.Core.Services.Image;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Models.Logging;

namespace KMOD;

public record MyModMetadata : AbstractModMetadata
{
	public override string? Name { get; set; } = "KMOD";
	public override string? Author { get; set; } = "Krinkels";
	public override List<string>? Contributors { get; set; } = new() { "", "" };
	public override string? Version { get; set; } = "1.2.0";
	public override string? SptVersion { get; set; } = "4.0.0";
	public override List<string>? LoadBefore { get; set; } = null;
	public override List<string>? LoadAfter { get; set; } = null;
	public override List<string>? Incompatibilities { get; set; } = null;
	public override Dictionary<string, string>? ModDependencies { get; set; } = null;
	public override string? Url { get; set; } = "https://github.com/Krinkelss/kmod_sharp";
	public override bool? IsBundleMod { get; set; } = false;
	public override string? Licence { get; set; } = "MIT";
}

[Injectable( TypePriority = OnLoadOrder.PostDBModLoader )]
public class KMOD(
	//ISptLogger<KMOD> logger,
	//HashUtil hashUtil,
	DatabaseService databaseService,
	//TraderController traderController,
	ConfigServer _configServer
) : IOnLoad
{
	//_props - Properties
	public Task OnLoad()
	{
		Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();
		SPTarkov.Server.Core.Models.Eft.Common.Config globals = databaseService.GetGlobals().Configuration;
		RagfairConfig Ragfair = _configServer.GetConfig<RagfairConfig>();
		LocationConfig locs = _configServer.GetConfig<LocationConfig>();
		BotConfig botConfig = _configServer.GetConfig<BotConfig>();
		SPTarkov.Server.Core.Models.Spt.Server.Locations locations = databaseService.GetLocations();
		SPTarkov.Server.Core.Models.Spt.Hideout.Hideout hideout = databaseService.GetHideout();
		TraderConfig traderConfig = _configServer.GetConfig<TraderConfig>();

		// Возможность продавать биткоин на барахолке
		items[ ItemTpl.BARTER_PHYSICAL_BITCOIN ].Properties.CanSellOnRagfair = true;

		// Surv12, меняем время применения, было 20 стало 10
		items[ ItemTpl.MEDICAL_SURV12_FIELD_SURGICAL_KIT ].Properties.MedUseTime = 10;

		// У всех торговцев товар "найден в рейде"
		//trader.purchasesAreFoundInRaid = true;
		traderConfig.PurchasesAreFoundInRaid = true;

		// Свой ник в списке имён ботов
		//DB.getBots().types.bear[ "firstName" ].push( "Zloy Tapok" );
		//DB.getBots().types.usec[ "firstName" ].push( "Zloy Tapok" );

		///****************************************************************
		int StackMaxSize = 150;                 // Насколько увеличить число патронов в ячейке
		int LoadUnloadModifier = -30;           // Отрицательное число уменьшает время заряда магазина
		foreach( var id in items.Keys )
		{
			var baseItem = items[ id ];

			// Убрать лимит ключей
			if( ( baseItem.Parent == BaseClasses.KEY_MECHANICAL || baseItem.Parent == BaseClasses.KEYCARD ) && baseItem.Properties?.MaximumNumberOfUsage != null )
			{
				if( baseItem.Properties?.MaximumNumberOfUsage == 1 )
					continue;

				baseItem.Properties.MaximumNumberOfUsage = 0;
			}

			// Без перегрева оружия
			if( baseItem.Properties?.AllowOverheat != null )
			{
				baseItem.Properties.AllowOverheat = false;
			}

			// Насколько увеличить число патронов в ячейке
			if( StackMaxSize > 0 && baseItem.Parent == BaseClasses.AMMO && baseItem.Properties.StackMaxSize != null )
			{
				baseItem.Properties.StackMaxSize += StackMaxSize;
			}

			// Процент уменьшения/увеличения времени зарядки магазина
			if( baseItem.Parent == BaseClasses.MAGAZINE && baseItem.Properties?.LoadUnloadModifier != null )
			{
				baseItem.Properties.LoadUnloadModifier = LoadUnloadModifier;
			}
		}
		///****************************************************************

		///****************************************************************
		// Ремонт не изнашивает броню
		foreach( var armormats in globals.ArmorMaterials.Values )
		{
			armormats.MaxRepairDegradation = 0;
			armormats.MinRepairDegradation = 0;
			armormats.MaxRepairKitDegradation = 0;
			armormats.MinRepairKitDegradation = 0;
		}

		// Ремонт не изнашивает оружие
		foreach( var item in items.Values )
		{
			if( item.Properties?.MaxRepairDegradation != null && item.Properties?.MaxRepairKitDegradation != null )
			{
				item.Properties.MinRepairDegradation = 0;
				item.Properties.MaxRepairDegradation = 0;
				item.Properties.MinRepairKitDegradation = 0;
				item.Properties.MaxRepairKitDegradation = 0;
			}
		}

		///****************************************************************
		///****************************************************************
		// Отключить чёрный список BSG
		Ragfair.Dynamic.Blacklist.EnableBsgList = false;

		// Шанс продажи на барахолке
		Ragfair.Sell.Chance.Base = 100;

		// Шанс продажи за дорого
		Ragfair.Sell.Chance.MaxSellChancePercent = 100;

		// Шанс продажи за дёшево
		Ragfair.Sell.Chance.MinSellChancePercent = 100;

		// Максимальное
		Ragfair.Sell.Time.Max = 0.5;

		// Минимальное
		Ragfair.Sell.Time.Min = 0;

		// Товары на барахолке "найдены в рейде"
		Ragfair.Dynamic.PurchasesAreFoundInRaid = true;
		///****************************************************************

		///****************************************************************
		// Шанс появления динамического лута
		locs.LooseLootMultiplier[ "factory4_day" ] += 2;
		locs.LooseLootMultiplier[ "factory4_night" ] += 2;
		locs.LooseLootMultiplier[ "bigmap" ] += 2;
		locs.LooseLootMultiplier[ "woods" ] += 2;
		locs.LooseLootMultiplier[ "shoreline" ] += 2;
		locs.LooseLootMultiplier[ "sandbox" ] += 2;
		locs.LooseLootMultiplier[ "sandbox_high" ] += 2;
		locs.LooseLootMultiplier[ "interchange" ] += 2;
		locs.LooseLootMultiplier[ "lighthouse" ] += 2;
		locs.LooseLootMultiplier[ "laboratory" ] += 2;
		locs.LooseLootMultiplier[ "rezervbase" ] += 2;
		locs.LooseLootMultiplier[ "tarkovstreets" ] += 2;
		locs.LooseLootMultiplier[ "labyrinth" ] += 2;

		locs.ContainerRandomisationSettings.Enabled = false;
		///****************************************************************

		///****************************************************************
		// Бесконечная выносливость
		globals.Stamina.Capacity = 500;
		globals.Stamina.BaseRestorationRate = 500;
		globals.Stamina.StaminaExhaustionCausesJiggle = false;
		globals.Stamina.StaminaExhaustionStartsBreathSound = false;
		globals.Stamina.StaminaExhaustionRocksCamera = false;
		globals.Stamina.SprintDrainRate = 0;
		globals.Stamina.JumpConsumption = 0;
		globals.Stamina.AimDrainRate = 0;
		globals.Stamina.SitToStandConsumption = 0;

		// Множитель опыта навыкам
		globals.SkillsSettings.SkillProgressRate = 10;

		// Множитель прокачки оружия
		globals.SkillsSettings.WeaponSkillProgressRate = 5;

		// Сколько длится усталость
		globals.SkillFatigueReset = 0;

		// Очки бодрости
		globals.SkillFreshPoints = 10;

		// % эффективности "свежих" навыков
		globals.SkillFreshEffectiveness = 10;

		// Множитель опыта при бодрости
		globals.SkillFreshEffectiveness = 10;

		// Усталость за очко
		globals.SkillFatiguePerPoint = 0;

		// Опыт при максимальной усталости
		globals.SkillMinEffectiveness = 10;

		// Очки перед наступлением усталости
		globals.SkillPointsBeforeFatigue = 100;
		///****************************************************************

		///****************************************************************		
		// Словарь, который связывает ID карты с соответствующими точками выхода
		var entryPointsMap = new Dictionary<string, string>
		{
			{ "bigmap", "Customs,Boiler Tanks" },						// Таможня
			{ "interchange", "MallSE,MallNW" },							// Развязка
			{ "shoreline", "Village,Riverside" },						// Берег
			{ "woods", "House,Old Station" },							// Лес
			{ "lighthouse", "Tunnel,North" },							// Маяк
			{ "tarkovstreets", "E1_2,E6_1,E2_3,E3_4,E4_5,E5_6,E6_1" },	// Улицы таркова
			{ "sandbox", "west,east" },									// Эпицентр
			{ "sandbox_high", "west,east" }								// Эпицентр
		};

		var mapsDb = databaseService.GetLocations();
		var mapsDict = mapsDb.GetDictionary();
		foreach( var (key, cap) in botConfig.MaxBotCap )
		{
			if( !mapsDict.TryGetValue( mapsDb.GetMappedKey( key ), out var map ) )
			{
				continue;
			}

			// Выход с любой стороны
			var mapId = map.Base.Id.ToLower();
			if( entryPointsMap.TryGetValue( mapId, out var entryPoints ) )
			{
				foreach( var extract in map.Base.Exits )
				{
					extract.EntryPoints = entryPoints;

					// Выход на машине					
					if( extract.PassageRequirement == RequirementState.TransferItem )
					{
						extract.ExfiltrationTime = 10;  // 10 Секунд на ожидание
					}
				}
			}

			foreach( var extract in map.Base.Exits )
			{
				// Выходы с шансом всегда доступны
				if( extract.Name != "EXFIL_Train" )
				{
					extract.Chance = 100;
				}

				// Совместный выход
				if( extract.RequirementTip == "EXFIL_Cooperate" /*или "PassageRequirement": "ScavCooperation"*/ )
				{
					extract.PassageRequirement = RequirementState.None;
					extract.ExfiltrationType = ExfiltrationType.Individual;
					extract.Id = "";
					extract.Count = 0;
					extract.PlayersCount = 0;
					extract.RequirementTip = "";
					//! Протестировать
					if( extract.RequiredSlot != null )
					{
						extract.RequiredSlot = EquipmentSlots.SecuredContainer;
					}
				}
			}
		}
		///****************************************************************

		///****************************************************************
		// Множитель времени постройки в убежище. Меньше - быстрее. При -1 мгновенная постройка
		foreach( var area in hideout.Areas )
		{
			foreach( var stage in area.Stages.Values )
			{
				stage.ConstructionTime = 0;
			}
		}

		// Множитель времени производства
		foreach( var recipe in hideout.Production.Recipes )
		{
			if( recipe.Continuous == false && recipe.ProductionTime > 10 )
			{
				recipe.ProductionTime = 10;
			}
		}
		///****************************************************************
		///https://github.com/sp-tarkov/server-csharp/pull/251
		// Сидорович


		//logger.Info( "	------------------------------End" );

		return Task.CompletedTask;
	}
}

[Injectable( TypePriority = OnLoadOrder.PostDBModLoader + 1 )]
public class KTRADER_SIDR(
	KTRADER TRADER // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
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
	KTRADER TRADER // This is a custom class we add for this mod, we made it injectable so it can be accessed like other classes here
) : IOnLoad
{
	public Task OnLoad()
	{
		TRADER.MERCHANT_Enable();
		return Task.CompletedTask;
	}
}
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