using System;
using Bonkadoo.Application;
using Bonkadoo.Infrastructure.Settings;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace Bonkadoo.Infrastructure.Unity
{
    /// <summary>
    /// One non-consumable "Remove Ads" product (ADS_AND_IAP). Ownership is cached in settings so it works
    /// offline; the platform store stays the authority when reachable. Errors never block play.
    /// </summary>
    public sealed class UnityIapStore : IStoreService, IDetailedStoreListener
    {
        // must match the product created in App Store Connect / Play Console (human step, D20)
        public const string ProductId = "com.omnicasa.bonkadoo.removeads";

        private readonly SettingsStore _settings;
        private IStoreController _controller;
        private IExtensionProvider _extensions;
        private Action<PurchaseResult> _pendingPurchase;

        public string RemoveAdsProductId => ProductId;
        public bool IsReady => _controller != null;
        public string LocalizedPrice => _controller?.products.WithID(ProductId)?.metadata.localizedPriceString;
        public string InitError { get; private set; }

        public UnityIapStore(SettingsStore settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
            }
            catch (Exception ex)
            {
                // IAP still initializes without Gaming Services; log and continue
                Debug.LogWarning("[Bonkadoo] UnityServices init failed: " + ex.Message);
            }
            try
            {
                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                builder.AddProduct(ProductId, ProductType.NonConsumable);
                UnityPurchasing.Initialize(this, builder);
            }
            catch (Exception ex)
            {
                InitError = ex.Message;
            }
        }

        public void PurchaseRemoveAds(Action<PurchaseResult> done)
        {
            if (_settings.RemoveAdsOwned) { done?.Invoke(PurchaseResult.Purchased); return; }
            if (_controller == null) { done?.Invoke(PurchaseResult.Unavailable); return; }
            _pendingPurchase = done;
            _controller.InitiatePurchase(ProductId);
        }

        public void RestorePurchases(Action<bool> done)
        {
            if (_extensions == null) { done?.Invoke(false); return; }
            if (UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer ||
                UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                _extensions.GetExtension<IAppleExtensions>().RestoreTransactions((ok, _) =>
                    done?.Invoke(ok && _settings.RemoveAdsOwned));
                return;
            }
            // Google Play and the editor fake store re-deliver owned non-consumables on initialization
            SyncOwnership();
            done?.Invoke(_settings.RemoveAdsOwned);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
            SyncOwnership();
        }

        private void SyncOwnership()
        {
            var product = _controller?.products.WithID(ProductId);
            if (product == null) return;
            if (product.hasReceipt && !_settings.RemoveAdsOwned) _settings.SetRemoveAdsOwned(true);
            // refunded/revoked: the store answered and no longer reports ownership
            else if (!product.hasReceipt && _settings.RemoveAdsOwned && UnityEngine.Application.platform == RuntimePlatform.Android)
                _settings.SetRemoveAdsOwned(false);
        }

        public void OnInitializeFailed(InitializationFailureReason error) => InitError = error.ToString();
        public void OnInitializeFailed(InitializationFailureReason error, string message) => InitError = $"{error}: {message}";

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (args.purchasedProduct.definition.id == ProductId)
            {
                _settings.SetRemoveAdsOwned(true);
                Complete(PurchaseResult.Purchased);
            }
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failure) =>
            Complete(failure.reason == PurchaseFailureReason.UserCancelled ? PurchaseResult.Cancelled : PurchaseResult.Failed);

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) =>
            Complete(reason == PurchaseFailureReason.UserCancelled ? PurchaseResult.Cancelled : PurchaseResult.Failed);

        private void Complete(PurchaseResult result)
        {
            var cb = _pendingPurchase;
            _pendingPurchase = null;
            cb?.Invoke(result);
        }
    }
}
