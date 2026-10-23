using UnityEngine;

public class EconomySystem : MonoBehaviour
{
    public static EconomySystem Instance { get; private set; }

    public int startingMoney = 750;
    public int Money { get; private set; }

    const string MoneyKey = "MiniGTA_V6_Money";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Money = GameSession.IsTest ? startingMoney : PlayerPrefs.GetInt(MoneyKey, startingMoney);
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        Save();
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (Money < amount) return false;
        Money -= amount;
        Save();
        return true;
    }

    public void Save()
    {
        if (GameSession.IsTest) return;
        PlayerPrefs.SetInt(MoneyKey, Mathf.Max(0, Money));
        PlayerPrefs.Save();
    }

    public void ResetEconomy()
    {
        Money = startingMoney;
        Save();
    }

    void OnApplicationQuit() => Save();
    public void Restore(int amount) { Money = Mathf.Clamp(amount,0,100000000); }
    void OnDestroy() { if(Instance==this) Instance=null; }
}
