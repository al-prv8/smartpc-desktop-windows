using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SensePC.Desktop.WinUI.Services;
using System.Collections.Generic;
using System.Linq;

namespace SensePC.Desktop.WinUI.Views
{
    public sealed partial class BillingPage : Page
    {
        private readonly SensePCApiService _apiService;
        private List<PaymentMethod> _paymentMethods = new();
        private List<StoragePricingTier> _storageTiers = new();
        private decimal _selectedRechargeAmount;
        private bool _isLoading;

        // Quick recharge amounts from API or defaults
        private readonly decimal[] _defaultRechargeAmounts = { 20m, 50m, 100m, 200m };

        public BillingPage()
        {
            this.InitializeComponent();
            _apiService = new SensePCApiService(new SecureStorage());
            this.Loaded += BillingPage_Loaded;
        }

        private async void BillingPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
        }

        private async System.Threading.Tasks.Task LoadAllDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                // Load all data in parallel
                var balanceTask = _apiService.GetBillingBalanceAsync();
                var spendingTask = _apiService.GetMonthlySpendingAsync();
                var paymentMethodsTask = _apiService.GetPaymentMethodsAsync();
                var autoRechargeTask = _apiService.GetAutoRechargeAsync();
                var storagePricingTask = _apiService.GetStoragePricingAsync();
                var rechargeHistoryTask = _apiService.GetRechargeHistoryAsync(20);

                await System.Threading.Tasks.Task.WhenAll(
                    balanceTask, spendingTask, paymentMethodsTask, 
                    autoRechargeTask, storagePricingTask, rechargeHistoryTask);

                // Update Balance
                var balance = await balanceTask;
                if (balance != null)
                {
                    BalanceText.Text = $"${balance.Balance:N2}";
                    PromoText.Text = $"${balance.PromoBalance:N2}";
                    CashbackText.Text = $"${balance.Cashback:N2}";
                    
                    if (balance.LastRecharge?.Timestamp != null && 
                        System.DateTime.TryParse(balance.LastRecharge.Timestamp, out var lastRechargeDate))
                    {
                        LastRechargeText.Text = $"Last recharged: {lastRechargeDate:MMM dd, yyyy}";
                    }
                    else
                    {
                        LastRechargeText.Text = "No recharge history yet";
                    }
                }

                // Update Monthly Spending
                var spending = await spendingTask;
                if (spending != null)
                {
                    SpendingText.Text = $"${spending.CurrentMonth:N2}";
                    var sign = spending.PercentChange >= 0 ? "+" : "";
                    SpendingChangeText.Text = $"{sign}{spending.PercentChange:N1}% from last month";
                    
                    // Update trend icon
                    if (spending.Trend == "increase")
                    {
                        TrendIcon.Glyph = "\uE70E"; // Up arrow
                        TrendIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    }
                    else if (spending.Trend == "decrease")
                    {
                        TrendIcon.Glyph = "\uE70D"; // Down arrow
                        TrendIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    }
                    else
                    {
                        TrendIcon.Glyph = "\uE738"; // Minus/unchanged
                    }
                }

                // Update Payment Methods
                var paymentMethods = await paymentMethodsTask;
                if (paymentMethods?.PaymentMethods != null)
                {
                    _paymentMethods = paymentMethods.PaymentMethods;
                    PaymentMethodCombo.ItemsSource = _paymentMethods;
                    PaymentMethodsListView.ItemsSource = _paymentMethods;
                    
                    // Show/hide empty state
                    NoPaymentMethodsPanel.Visibility = _paymentMethods.Count == 0 
                        ? Visibility.Visible : Visibility.Collapsed;
                    PaymentMethodsListView.Visibility = _paymentMethods.Count > 0 
                        ? Visibility.Visible : Visibility.Collapsed;
                    
                    // Select default payment method
                    var defaultMethod = _paymentMethods.FirstOrDefault(p => p.IsDefault) 
                                        ?? _paymentMethods.FirstOrDefault();
                    if (defaultMethod != null)
                    {
                        PaymentMethodCombo.SelectedItem = defaultMethod;
                    }
                }
                else
                {
                    NoPaymentMethodsPanel.Visibility = Visibility.Visible;
                    PaymentMethodsListView.Visibility = Visibility.Collapsed;
                }

                // Update Auto-Recharge Settings
                var autoRecharge = await autoRechargeTask;
                if (autoRecharge != null)
                {
                    AutoRechargeToggle.Toggled -= AutoRechargeToggle_Toggled;
                    AutoRechargeToggle.IsOn = autoRecharge.Enabled;
                    AutoRechargeToggle.Toggled += AutoRechargeToggle_Toggled;
                    
                    AutoRechargeSettingsPanel.Visibility = autoRecharge.Enabled 
                        ? Visibility.Visible : Visibility.Collapsed;
                    
                    ThresholdBox.Value = (double)autoRecharge.Threshold;
                    AutoRechargeAmountBox.Value = (double)autoRecharge.RechargeAmount;
                }

                // Update Storage Pricing (for internal data use - UI now uses tab slider)
                var storagePricing = await storagePricingTask;
                if (storagePricing != null && storagePricing.Count > 0)
                {
                    _storageTiers = storagePricing;
                }

                // Populate recharge amount buttons
                PopulateRechargeAmounts();

                // Update Recharge History
                var rechargeHistory = await rechargeHistoryTask;
                RechargeListView.ItemsSource = rechargeHistory;
                NoRechargeText.Visibility = rechargeHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllData error: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _isLoading = false;
            }
        }

        private void PopulateRechargeAmounts()
        {
            var amounts = new List<RechargeAmountItem>();
            foreach (var amount in _defaultRechargeAmounts)
            {
                amounts.Add(new RechargeAmountItem { Amount = amount, Display = $"${amount:N0}" });
            }
            RechargeAmountsListView.ItemsSource = amounts;
        }

        #region Event Handlers

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
        }

        private async void ManagePaymentMethods_Click(object sender, RoutedEventArgs e)
        {
            // Create the payment methods management dialog content
            var content = new StackPanel { Spacing = 16, MinWidth = 450 };
            
            // Show loading first
            var loadingText = new TextBlock { Text = "Loading payment methods...", FontStyle = Windows.UI.Text.FontStyle.Italic };
            content.Children.Add(loadingText);

            var dialog = new ContentDialog
            {
                Title = "Manage Payment Methods",
                Content = content,
                PrimaryButtonText = "Add New Card",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            // Load payment methods
            var paymentMethods = await _apiService.GetPaymentMethodsAsync();
            content.Children.Clear();

            if (paymentMethods?.PaymentMethods != null && paymentMethods.PaymentMethods.Count > 0)
            {
                // Security banner
                var securityBanner = new Border 
                { 
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green) { Opacity = 0.1 },
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12)
                };
                var securityContent = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 8 };
                securityContent.Children.Add(new FontIcon { Glyph = "\uE72E", FontSize = 14, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green) });
                securityContent.Children.Add(new TextBlock { Text = "Your cards are securely stored by Stripe", VerticalAlignment = VerticalAlignment.Center });
                securityBanner.Child = securityContent;
                content.Children.Add(securityBanner);

                // Cards list
                foreach (var card in paymentMethods.PaymentMethods)
                {
                    var cardBorder = new Border
                    {
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16)
                    };
                    
                    var cardGrid = new Grid();
                    cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    
                    // Card icon
                    var iconBorder = new Border
                    {
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
                        CornerRadius = new CornerRadius(6),
                        Width = 40, Height = 40,
                        Margin = new Thickness(0, 0, 12, 0)
                    };
                    iconBorder.Child = new FontIcon { Glyph = "\uE8C7", FontSize = 18, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(iconBorder, 0);
                    cardGrid.Children.Add(iconBorder);
                    
                    // Card info
                    var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    var cardTitle = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 8 };
                    cardTitle.Children.Add(new TextBlock { Text = card.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                    if (card.IsDefault)
                    {
                        var defaultBadge = new Border
                        {
                            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green) { Opacity = 0.2 },
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2)
                        };
                        defaultBadge.Child = new TextBlock { Text = "Default", FontSize = 11, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green) };
                        cardTitle.Children.Add(defaultBadge);
                    }
                    infoStack.Children.Add(cardTitle);
                    infoStack.Children.Add(new TextBlock { Text = card.ExpiryDisplay, FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                    Grid.SetColumn(infoStack, 1);
                    cardGrid.Children.Add(infoStack);
                    
                    // Actions
                    var actionsPanel = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 8 };
                    
                    if (!card.IsDefault)
                    {
                        var setDefaultBtn = new Button { Content = "Set Default", Tag = card.Id };
                        setDefaultBtn.Click += async (s, args) =>
                        {
                            dialog.Hide();
                            await SetPaymentMethodAsDefault(card.Id);
                        };
                        actionsPanel.Children.Add(setDefaultBtn);
                    }
                    
                    var removeBtn = new Button { Content = "Remove", Tag = card.Id };
                    removeBtn.Click += async (s, args) =>
                    {
                        dialog.Hide();
                        await RemovePaymentMethodWithConfirmation(card.Id, card.DisplayName);
                    };
                    actionsPanel.Children.Add(removeBtn);
                    
                    Grid.SetColumn(actionsPanel, 2);
                    cardGrid.Children.Add(actionsPanel);
                    
                    cardBorder.Child = cardGrid;
                    content.Children.Add(cardBorder);
                }
            }
            else
            {
                // No cards state
                var emptyPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 12 };
                emptyPanel.Children.Add(new FontIcon { Glyph = "\uE8C7", FontSize = 48, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], HorizontalAlignment = HorizontalAlignment.Center });
                emptyPanel.Children.Add(new TextBlock { Text = "No payment methods saved yet", HorizontalAlignment = HorizontalAlignment.Center });
                emptyPanel.Children.Add(new TextBlock { Text = "Add a card to get started", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], HorizontalAlignment = HorizontalAlignment.Center });
                content.Children.Add(emptyPanel);
            }

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Open Add Card dialog
                AddPaymentMethod_Click(null, null);
            }
        }

        private async System.Threading.Tasks.Task SetPaymentMethodAsDefault(string paymentMethodId)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            var success = await _apiService.SetDefaultPaymentMethodAsync(paymentMethodId);
            LoadingOverlay.Visibility = Visibility.Collapsed;
            
            if (success)
            {
                await ShowSuccessDialogAsync("Payment method set as default.");
                await LoadAllDataAsync();
            }
            else
            {
                await ShowErrorDialogAsync("Failed to set default payment method.");
            }
        }

        private async System.Threading.Tasks.Task RemovePaymentMethodWithConfirmation(string paymentMethodId, string displayName)
        {
            var confirmDialog = new ContentDialog
            {
                Title = "Remove Payment Method?",
                Content = $"Are you sure you want to remove {displayName}? You can add it again at any time.",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                var success = await _apiService.DetachPaymentMethodAsync(paymentMethodId);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                
                if (success)
                {
                    await ShowSuccessDialogAsync("Payment method removed.");
                    await LoadAllDataAsync();
                }
                else
                {
                    await ShowErrorDialogAsync("Failed to remove payment method.");
                }
            }
        }

        private void RechargeAmount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RechargeAmountsListView.SelectedItem is RechargeAmountItem item)
            {
                _selectedRechargeAmount = item.Amount;
                CustomAmountBox.Value = double.NaN; // Clear custom amount
            }
        }

        private void CustomRecharge_Click(object sender, RoutedEventArgs e)
        {
            if (!double.IsNaN(CustomAmountBox.Value) && CustomAmountBox.Value >= 5)
            {
                _selectedRechargeAmount = (decimal)CustomAmountBox.Value;
                RechargeAmountsListView.SelectedItem = null; // Deselect preset
            }
        }

        private void StorageTierSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (StorageTierText == null || StoragePriceText == null) return;
            
            int tier = (int)e.NewValue;
            int sizeGB = tier * 20; // Each tier is 20GB
            decimal price = tier * 0.25m; // $0.25 per tier
            
            StorageTierText.Text = $"Selected: T-{tier} ({sizeGB} GB)";
            StoragePriceText.Text = $"${price:N2} / month";
        }

        private async void RechargeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRechargeAmount <= 0)
            {
                if (!double.IsNaN(CustomAmountBox.Value) && CustomAmountBox.Value >= 5)
                {
                    _selectedRechargeAmount = (decimal)CustomAmountBox.Value;
                }
                else
                {
                    await ShowErrorDialogAsync("Please select or enter an amount to recharge.");
                    return;
                }
            }

            var paymentMethod = PaymentMethodCombo.SelectedItem as PaymentMethod;
            if (paymentMethod == null)
            {
                await ShowErrorDialogAsync("Please select a payment method.");
                return;
            }

            // Confirm recharge
            var confirmDialog = new ContentDialog
            {
                Title = "Confirm Recharge",
                Content = $"Add ${_selectedRechargeAmount:N2} to your wallet using {paymentMethod.DisplayName}?",
                PrimaryButtonText = "Confirm",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            // Process recharge
            LoadingOverlay.Visibility = Visibility.Visible;
            var rechargeResult = await _apiService.RechargeWalletAsync(_selectedRechargeAmount, paymentMethod.Id);
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (rechargeResult?.Success == true)
            {
                await ShowSuccessDialogAsync($"Successfully added ${_selectedRechargeAmount:N2} to your wallet!");
                _selectedRechargeAmount = 0;
                await LoadAllDataAsync(); // Refresh data
            }
            else
            {
                await ShowErrorDialogAsync(rechargeResult?.ErrorMessage ?? "Failed to process recharge. Please try again.");
            }
        }

        private void AutoRechargeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            AutoRechargeSettingsPanel.Visibility = AutoRechargeToggle.IsOn 
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SaveAutoRecharge_Click(object sender, RoutedEventArgs e)
        {
            var enabled = AutoRechargeToggle.IsOn;
            var threshold = (decimal)ThresholdBox.Value;
            var rechargeAmount = (decimal)AutoRechargeAmountBox.Value;

            if (enabled && (threshold < 5 || rechargeAmount < 10))
            {
                await ShowErrorDialogAsync("Threshold must be at least $5 and recharge amount at least $10.");
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            var success = await _apiService.UpdateAutoRechargeAsync(enabled, threshold, rechargeAmount);
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (success)
            {
                await ShowSuccessDialogAsync("Auto-recharge settings saved successfully!");
            }
            else
            {
                await ShowErrorDialogAsync("Failed to save auto-recharge settings. Please try again.");
            }
        }

        private async void HistoryTab_Checked(object sender, RoutedEventArgs e)
        {
            if (RechargeTab == null) return;

            // Hide all panels
            RechargeHistoryPanel.Visibility = Visibility.Collapsed;
            PCUsageHistoryPanel.Visibility = Visibility.Collapsed;
            CloudUsageHistoryPanel.Visibility = Visibility.Collapsed;

            if (RechargeTab.IsChecked == true)
            {
                RechargeHistoryPanel.Visibility = Visibility.Visible;
                await LoadRechargeHistoryAsync();
            }
            else if (PCUsageTab.IsChecked == true)
            {
                PCUsageHistoryPanel.Visibility = Visibility.Visible;
                await LoadPCUsageHistoryAsync();
            }
            else if (CloudUsageTab.IsChecked == true)
            {
                CloudUsageHistoryPanel.Visibility = Visibility.Visible;
            }
        }

        private async void AddPaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            // Create card entry form
            var cardForm = new StackPanel { Spacing = 16, MinWidth = 350 };
            
            var nameBox = new TextBox { PlaceholderText = "Cardholder Name", Header = "Name on Card" };
            var cardNumberBox = new TextBox { PlaceholderText = "1234 5678 9012 3456", Header = "Card Number", MaxLength = 19 };
            
            var expiryPanel = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 12 };
            var monthBox = new NumberBox { PlaceholderText = "MM", Header = "Month", Minimum = 1, Maximum = 12, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 100 };
            var yearBox = new NumberBox { PlaceholderText = "YYYY", Header = "Year", Minimum = 2024, Maximum = 2040, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 100 };
            expiryPanel.Children.Add(monthBox);
            expiryPanel.Children.Add(yearBox);
            
            var cvcBox = new PasswordBox { PlaceholderText = "123", Header = "CVC", MaxLength = 4, Width = 100 };
            var setDefaultCheck = new CheckBox { Content = "Set as default payment method", IsChecked = _paymentMethods.Count == 0 };
            
            // Security note
            var securityNote = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 8 };
            var lockIcon = new FontIcon { Glyph = "\uE72E", FontSize = 14, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green) };
            var securityText = new TextBlock { Text = "Card data is securely processed via Stripe", 
                                               Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                                               VerticalAlignment = VerticalAlignment.Center };
            securityNote.Children.Add(lockIcon);
            securityNote.Children.Add(securityText);
            
            cardForm.Children.Add(nameBox);
            cardForm.Children.Add(cardNumberBox);
            cardForm.Children.Add(expiryPanel);
            cardForm.Children.Add(cvcBox);
            cardForm.Children.Add(setDefaultCheck);
            cardForm.Children.Add(securityNote);

            var dialog = new ContentDialog
            {
                Title = "Add Payment Method",
                Content = cardForm,
                PrimaryButtonText = "Add Card",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            // Validate input
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                await ShowErrorDialogAsync("Please enter the cardholder name.");
                return;
            }
            if (string.IsNullOrWhiteSpace(cardNumberBox.Text) || cardNumberBox.Text.Replace(" ", "").Length < 13)
            {
                await ShowErrorDialogAsync("Please enter a valid card number.");
                return;
            }
            if (double.IsNaN(monthBox.Value) || double.IsNaN(yearBox.Value))
            {
                await ShowErrorDialogAsync("Please enter the expiry date.");
                return;
            }
            if (string.IsNullOrWhiteSpace(cvcBox.Password) || cvcBox.Password.Length < 3)
            {
                await ShowErrorDialogAsync("Please enter a valid CVC.");
                return;
            }

            // Submit to API
            LoadingOverlay.Visibility = Visibility.Visible;
            var addResult = await _apiService.AddPaymentMethodAsync(
                cardNumberBox.Text,
                (int)monthBox.Value,
                (int)yearBox.Value,
                cvcBox.Password,
                nameBox.Text,
                setDefaultCheck.IsChecked == true);
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (addResult.Success)
            {
                await ShowSuccessDialogAsync("Payment method added successfully!");
                await LoadAllDataAsync(); // Refresh list
            }
            else
            {
                await ShowErrorDialogAsync(addResult.ErrorMessage ?? "Failed to add payment method. Please check your card details.");
            }
        }

        private async void SetDefaultPaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string paymentMethodId)
            {
                // Check if already default
                var method = _paymentMethods.FirstOrDefault(p => p.Id == paymentMethodId);
                if (method?.IsDefault == true)
                {
                    await ShowSuccessDialogAsync("This card is already your default payment method.");
                    return;
                }

                LoadingOverlay.Visibility = Visibility.Visible;
                var success = await _apiService.SetDefaultPaymentMethodAsync(paymentMethodId);
                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (success)
                {
                    await ShowSuccessDialogAsync("Default payment method updated!");
                    await LoadAllDataAsync(); // Refresh to show new default
                }
                else
                {
                    await ShowErrorDialogAsync("Failed to update default payment method. Please try again.");
                }
            }
        }

        private async void RemovePaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string paymentMethodId)
            {
                var method = _paymentMethods.FirstOrDefault(p => p.Id == paymentMethodId);
                
                // Confirm removal
                var confirmDialog = new ContentDialog
                {
                    Title = "Remove Payment Method",
                    Content = $"Are you sure you want to remove {method?.DisplayName ?? "this card"}?",
                    PrimaryButtonText = "Remove",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                LoadingOverlay.Visibility = Visibility.Visible;
                var success = await _apiService.DetachPaymentMethodAsync(paymentMethodId);
                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (success)
                {
                    await ShowSuccessDialogAsync("Payment method removed successfully!");
                    await LoadAllDataAsync(); // Refresh list
                }
                else
                {
                    await ShowErrorDialogAsync("Failed to remove payment method. Please try again.");
                }
            }
        }

        private void HistorySearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Filter history based on search text
            // Implementation depends on which tab is active
        }

        private void HistoryDateRange_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            // Filter history by date range
            FilterHistoryByDateRange();
        }

        private void ClearDateRange_Click(object sender, RoutedEventArgs e)
        {
            HistoryFromDate.Date = null;
            HistoryToDate.Date = null;
            // Reset to show all history
            FilterHistoryByDateRange();
        }

        private void FilterHistoryByDateRange()
        {
            var fromDate = HistoryFromDate.Date?.Date;
            var toDate = HistoryToDate.Date?.Date;
            
            // Apply date filter to the currently visible history tab
            // This would filter RechargeListView, PCUsageListView, or CloudUsageListView
            // based on the fromDate and toDate values
            System.Diagnostics.Debug.WriteLine($"Filter: {fromDate} to {toDate}");
        }

        #endregion

        #region Helper Methods

        private async System.Threading.Tasks.Task LoadRechargeHistoryAsync()
        {
            var recharges = await _apiService.GetRechargeHistoryAsync(20);
            RechargeListView.ItemsSource = recharges;
            NoRechargeText.Visibility = recharges.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task LoadPCUsageHistoryAsync()
        {
            var usages = await _apiService.GetUsageHistoryAsync(20);
            PCUsageListView.ItemsSource = usages;
            NoPCUsageText.Visibility = usages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task ShowErrorDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async System.Threading.Tasks.Task ShowSuccessDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Success",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        #endregion
    }

    /// <summary>
    /// Helper class for recharge amount display
    /// </summary>
    public class RechargeAmountItem
    {
        public decimal Amount { get; set; }
        public string Display { get; set; } = "";
        public override string ToString() => Display;
    }
}
