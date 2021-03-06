﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _coinList;
		private string _buildTransactionButtonText;
		private bool _isMax;
		private string _amount;
		private bool _ignoreAmountChanges;
		private int _fee;
		private string _password;
		private string _address;
		private string _label;
		private bool _isBusy;
		private string _warningMessage;
		private string _successMessage;
		private const string BuildTransactionButtonTextString = "Send Transaction";
		private const string BuildingTransactionButtonTextString = "Sending Transaction...";

		public SendTabViewModel(WalletViewModel walletViewModel)
			: base("Send", walletViewModel)
		{
			var onCoinsSetModified = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.HashSetChanged))
				.ObserveOn(RxApp.MainThreadScheduler);

			var globalCoins = Global.WalletService.Coins.CreateDerivedCollection(c => new CoinViewModel(c), null, (first, second) => second.Amount.CompareTo(first.Amount), signalReset: onCoinsSetModified, RxApp.MainThreadScheduler);
			globalCoins.ChangeTrackingEnabled = true;

			var filteredCoins = globalCoins.CreateDerivedCollection(c => c, c => !c.SpentOrCoinJoinInProcess);

			CoinList = new CoinListViewModel(filteredCoins);

			BuildTransactionButtonText = BuildTransactionButtonTextString;

			this.WhenAnyValue(x => x.Amount).Subscribe(_ =>
			{
				if (!_ignoreAmountChanges)
				{
					IsMax = false;
				}
			});

			BuildTransactionCommand = ReactiveCommand.Create(async () =>
			{
				IsBusy = true;
				try
				{
					if (string.IsNullOrWhiteSpace(Label))
					{
						throw new InvalidOperationException("Label is required.");
					}

					var selectedCoins = CoinList.Coins.Where(cvm => cvm.IsSelected).Select(cvm => new TxoRef(cvm.Model.TransactionId, cvm.Model.Index)).ToList();

					if (!selectedCoins.Any())
					{
						throw new InvalidOperationException("No coins are selected to spend.");
					}

					var address = BitcoinAddress.Create(Address, Global.Network);
					var script = address.ScriptPubKey;
					var amount = IsMax ? Money.Zero : Money.Parse(Amount);
					var operation = new WalletService.Operation(script, amount, Label);

					var result = await Task.Run(async () => await Global.WalletService.BuildTransactionAsync(Password, new[] { operation }, Fee, allowUnconfirmed: true, allowedInputs: selectedCoins));

					await Task.Run(async () => await Global.WalletService.SendTransactionAsync(result.Transaction));

					SuccessMessage = "Transaction is successfully sent!";
					WarningMessage = "";
				}
				catch (Exception ex)
				{
					SuccessMessage = "";
					WarningMessage = ex.ToTypeMessageString();
				}
				finally
				{
					IsBusy = false;
				}
			},
			this.WhenAny(x => x.IsMax, x => x.Amount, x => x.Address, x => x.IsBusy,
				(isMax, amount, address, busy) => ((isMax.Value || !string.IsNullOrWhiteSpace(amount.Value)) && !string.IsNullOrWhiteSpace(Address) && !IsBusy)));

			MaxCommand = ReactiveCommand.Create(() =>
			{
				SetMax();
			});

			this.WhenAnyValue(x => x.IsBusy).Subscribe(busy =>
			{
				if (busy)
				{
					BuildTransactionButtonText = BuildingTransactionButtonTextString;
				}
				else
				{
					BuildTransactionButtonText = BuildTransactionButtonTextString;
				}
			});
		}

		private void SetMax()
		{
			if (IsMax)
			{
				ResetMax();
				return;
			}

			IsMax = true;

			_ignoreAmountChanges = true;
			Amount = "All Selected Coins!";
			_ignoreAmountChanges = false;
		}

		private void ResetMax()
		{
			_isMax = false;

			_ignoreAmountChanges = true;
			Amount = "";
			_ignoreAmountChanges = false;
		}
		public CoinListViewModel CoinList
		{
			get { return _coinList; }
			set { this.RaiseAndSetIfChanged(ref _coinList, value); }
		}

		public bool IsBusy
		{
			get { return _isBusy; }
			set { this.RaiseAndSetIfChanged(ref _isBusy, value); }
		}

		public string BuildTransactionButtonText
		{
			get { return _buildTransactionButtonText; }
			set { this.RaiseAndSetIfChanged(ref _buildTransactionButtonText, value); }
		}

		public bool IsMax
		{
			get { return _isMax; }
			set { this.RaiseAndSetIfChanged(ref _isMax, value); }
		}

		public string Amount
		{
			get { return _amount; }
			set { this.RaiseAndSetIfChanged(ref _amount, value); }
		}

		public int Fee
		{
			get { return _fee; }
			set { this.RaiseAndSetIfChanged(ref _fee, value); }
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public string Address
		{
			get { return _address; }
			set { this.RaiseAndSetIfChanged(ref _address, value); }
		}

		public string Label
		{
			get { return _label; }
			set { this.RaiseAndSetIfChanged(ref _label, value); }
		}

		public string WarningMessage
		{
			get { return _warningMessage; }
			set { this.RaiseAndSetIfChanged(ref _warningMessage, value); }
		}

		public string SuccessMessage
		{
			get { return _successMessage; }
			set { this.RaiseAndSetIfChanged(ref _successMessage, value); }
		}

		public ReactiveCommand BuildTransactionCommand { get; }

		public ReactiveCommand MaxCommand { get; }
	}
}
