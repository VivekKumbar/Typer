using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// Universal In-App Purchasing (IAP) manager handling store initialization, purchase lifecycle,
/// callbacks, and UI purchase triggers. Self-instantiates as a persistent Singleton.
/// </summary>
[DisallowMultipleComponent]
public class IAPManager : MonoBehaviour, IDetailedStoreListener
{
    // =========================================================================
    // SINGLETON PATTERN (SELF-INITIALIZING & DONT DESTROY ON LOAD)
    // =========================================================================
    public static IAPManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindAnyObjectByType<IAPManager>();
                if (s_instance == null)
                {
                    var go = new GameObject("[IAPManager]");
                    s_instance = go.AddComponent<IAPManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return s_instance;
        }
        private set { s_instance = value; }
    }
    private static IAPManager s_instance;

    // =========================================================================
    // PRODUCT IDS
    // =========================================================================
    public const string ProductId100Coins = "com.mygame.100coins";
    public const string ProductIdRemoveAds = "com.mygame.removeads";

    // =========================================================================
    // STORE CONTROLLERS & PROVIDERS
    // =========================================================================
    private IStoreController storeController;
    private IExtensionProvider extensionProvider;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializePurchasing();
    }

    /// <summary>
    /// Checks if Unity Purchasing is initialized and ready for transactions.
    /// </summary>
    public bool IsInitialized()
    {
        return storeController != null && extensionProvider != null;
    }

    // =========================================================================
    // INITIALIZATION
    // =========================================================================
    /// <summary>
    /// Configures products and initializes the Unity Purchasing module.
    /// </summary>
    public void InitializePurchasing()
    {
        if (IsInitialized()) return;

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(ProductId100Coins, ProductType.Consumable);
        builder.AddProduct(ProductIdRemoveAds, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    // =========================================================================
    // IDetailedStoreListener CALLBACKS
    // =========================================================================
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;
        extensionProvider = extensions;
        Debug.Log("[IAPManager] Unity IAP Initialized successfully.");
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        OnInitializeFailed(error, null);
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"[IAPManager] Initialization failed. Reason: {error}. Message: {message}");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productId = args.purchasedProduct.definition.id;
        Debug.Log($"[IAPManager] Processing purchase for product: {productId}");

        if (string.Equals(productId, ProductId100Coins, StringComparison.Ordinal))
        {
            Debug.Log("[IAPManager] Coins purchased: com.mygame.100coins");
            // Add 100 coins to player inventory
            Wallet.Add(100);
        }
        else if (string.Equals(productId, ProductIdRemoveAds, StringComparison.Ordinal))
        {
            Debug.Log("[IAPManager] Ads removed: com.mygame.removeads");
            // Disable ads in game
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.adsEnabled = false;
            }
            PlayerPrefs.SetInt("NoAdsPurchased", 1);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogWarning($"[IAPManager] Unrecognized product ID: {productId}");
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogError($"[IAPManager] Purchase of '{product.definition.id}' failed. Reason: {failureReason}");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.LogError($"[IAPManager] Purchase of '{product.definition.id}' failed. Reason: {failureDescription.reason}. Message: {failureDescription.message}");
    }

    // =========================================================================
    // PURCHASING & RESTORING METHODS (UI API)
    // =========================================================================
    public void BuyProduct(string productId)
    {
        if (!IsInitialized())
        {
            Debug.LogWarning($"[IAPManager] Cannot buy '{productId}'. Purchasing is not initialized yet.");
            return;
        }

        Product product = storeController.products.WithID(productId);
        if (product != null && product.availableToPurchase)
        {
            Debug.Log($"[IAPManager] Initiating purchase for '{product.definition.id}'...");
            storeController.InitiatePurchase(product);
        }
        else
        {
            Debug.LogError($"[IAPManager] BuyProduct failed: Product '{productId}' is either not found or not available for purchase.");
        }
    }

    public void Buy100Coins()
    {
        BuyProduct(ProductId100Coins);
    }

    public void BuyRemoveAds()
    {
        BuyProduct(ProductIdRemoveAds);
    }

    public void RestorePurchases()
    {
        if (!IsInitialized())
        {
            Debug.LogWarning("[IAPManager] RestorePurchases failed. Purchasing not initialized.");
            return;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.tvOS)
        {
            Debug.Log("[IAPManager] Restoring purchases on Apple platform...");
            var apple = extensionProvider.GetExtension<IAppleExtensions>();
            apple.RestoreTransactions((result, message) =>
            {
                Debug.Log($"[IAPManager] Restore transactions result: {result}. Message: {message}");
            });
        }
        else
        {
            Debug.LogWarning($"[IAPManager] RestorePurchases is not supported on this platform ({Application.platform}).");
        }
    }
}
