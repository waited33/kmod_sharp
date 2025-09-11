using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KMOD;

//************************************************************************************
//************************Предметы****************************************************
/// <summary>
/// Возможность изменять размер защищённого контейнера
/// </summary>
public class SecureContainer
{
	/// <summary>
	/// Вкл/выкл настройки контейнера
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	/// Имя защищённого контейнера. Доступны значения:<br/>
	/// <value>SECURE_CONTAINER_ALPHA<br/></value>
	/// <value>SECURE_CONTAINER_BETA<br/></value>
	/// <value>SECURE_CONTAINER_EPSILON<br/></value>
	/// <value>SECURE_CONTAINER_GAMMA<br/></value>
	/// <value>SECURE_CONTAINER_GAMMA_TUE<br/></value>	
	/// <value>SECURE_CONTAINER_KAPPA<br/></value>
	/// <value>SECURE_CONTAINER_KAPPA_DESECRATED<br/></value>
	/// <value>SECURE_CONTAINER_THETA<br/></value>	
	/// </summary>
	public string? Secure_Container_Name { get; set; }

	/// <summary>
	/// Горизонтальный размер
	/// </summary>
	public int HSize { get; set; }

	/// <summary>
	/// Вертикальный размер
	/// </summary>
	public int VSize { get; set; }
}

/// <summary>
/// Разные настройки связанные с предметами
/// </summary>
public class ITEMS
{
	/// <summary>
	/// Вкл/выкл настройки предметов
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	/// Убрать лимит ключей
	/// </summary>
	public bool RemoveKeysUsageNumber { get; set; }

	/// <summary>
	/// Одноразовые ключи используются только единожды
	/// </summary>
	public bool AvoidSingleKeys { get; set; }

	/// <summary>
	/// Насколько увеличить число патронов в ячейке
	/// </summary>
	public int StackMaxSize { get; set; }

	/// <summary>
	/// Множитель времени постройки в убежище. Меньше - быстрее. При -1 мгновенная постройка
	/// </summary>
	public int HideoutConstMult { get; set; }

	/// <summary>
	/// Множитель времени производства. Меньше - быстрее
	/// </summary>
	public int HideoutProdMult { get; set; }

	/// <summary>
	/// Возможность продавать биткоин на барахолке
	/// </summary>
	public bool BitcoinSellOnRagfair { get; set; }

	/// <summary>
	/// Время применения Surv12
	/// </summary>
	public int Surv12UseTime { get; set; }

	/// <summary>
	/// У всех торговцев товар "найден в рейде"
	/// </summary>
	public bool TraderPurchasesFoundInRaid { get; set; }

	/// <summary>
	/// Ремонт не изнашивает броню
	/// </summary>
	public bool OpArmorRepair { get; set; }

	/// <summary>
	/// Ремонт не изнашивает оружие
	/// </summary>
	public bool OpGunRepair { get; set; }

	/// <summary>
	/// Защищённый контейнер
	/// </summary>
	public SecureContainer? SecureContainers { get; set; }
}
//************************************************************************************

//************************************************************************************
//************************Оружие******************************************************
public class WEAPON
{
	/// <summary>
	/// Вкл/выкл настройки оружия
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	/// Без перегрева оружия
	/// </summary>
	public bool WeaponHeatOff { get; set; }

	/// <summary>
	/// Процент уменьшения/увеличения времени зарядки магазина<br/>
	/// Отрицательное число уменьшает время заряда магазина
	/// </summary>
	public int LoadUnloadModifier { get; set; }
}
//************************************************************************************

//************************************************************************************
//************************Уменмя******************************************************
public class Skills
{
	/// <summary>
	/// Сколько длится усталость
	/// </summary>
	public int SkillFatigueReset { get; set; }

	/// <summary>
	/// Очки бодрости
	/// </summary>
	public int SkillFreshPoints { get; set; }

	/// <summary>
	/// % эффективности "свежих" навыков
	/// </summary>
	public int SkillFreshEffectiveness { get; set; }

	/// <summary>
	/// Усталость за очко
	/// </summary>
	public int SkillFatiguePerPoint { get; set; }

	/// <summary>
	/// Опыт при максимальной усталости
	/// </summary>
	public int SkillMinEffect { get; set; }

	/// <summary>
	/// Очки перед наступлением усталости
	/// </summary>
	public int SkillPointsBeforeFatigue { get; set; }
}

public class PLAYER
{
	/// <summary>
	/// Вкл/выкл настройки персонажа
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	/// Бесконечная выносливость
	/// </summary>
	public bool UnlimitedStamina { get; set; }

	/// <summary>
	/// Множитель опыта навыкам
	/// </summary>
	public int? SkillProgMult { get; set; }

	/// <summary>
	/// Множитель прокачки оружия
	/// </summary>
	public int WeaponSkillMult { get; set; }

	/// <summary>
	/// Прокачка умений персонажа
	/// </summary>
	public Skills? Skills { get; set; }
}
//************************************************************************************

public class KConfig
{
	public ITEMS? Items { get; set; }
	public WEAPON? Weapons { get; set; }
	public PLAYER? Player { get; set; }
}