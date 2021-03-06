﻿using Prism.Commands;
using Prism.Events;
using SimpleBusinessApp.Data.Repositories;
using SimpleBusinessApp.Event;
using SimpleBusinessApp.Wrapper;
using System.Threading.Tasks;
using System.Windows.Input;
using System;
using SimpleBusinessApp.Model;
using SimpleBusinessApp.View.Services;
using SimpleBusinessApp.Data.Lookups;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Data.Entity.Infrastructure;

namespace SimpleBusinessApp.ViewModel
{
    /// <summary>
    /// Loads a single Client
    /// </summary>
    public class ClientDetailViewModel : DetailViewModelBase, IClientDetailViewModel
    {
        private IClientRepository _clientRepository;

        private ICompanyLookupDataService _companyLookupDataService;
        private ClientWrapper _client;
        public ClientWrapper Client
        {
            get { return _client; }
            private set
            {
                _client = value;
                OnPropertyChanged();
            }
        }

        private ClientPhoneNumberWrapper _selectedPhoneNumber;

        public ClientPhoneNumberWrapper SelectedPhoneNumber
        {
            get { return _selectedPhoneNumber; }
            set
            {
                _selectedPhoneNumber = value;
                OnPropertyChanged();
                ((DelegateCommand)RemovePhoneNumberCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand AddPhoneNumberCommand { get; }

        public ICommand RemovePhoneNumberCommand { get; }

        public ObservableCollection<LookupItem> Companies { get; }
        public ObservableCollection<ClientPhoneNumberWrapper> PhoneNumbers { get; }

        public ClientDetailViewModel(IClientRepository clientRepository,
            IEventAggregator eventAggregator, IMessageDialogService messageDialogService,
            ICompanyLookupDataService companyLookupDataService) : base(eventAggregator, messageDialogService)
        {
            _clientRepository = clientRepository;
            _companyLookupDataService = companyLookupDataService;

            eventAggregator.GetEvent<AfterCollectionSavedEvent>()
                .Subscribe(AfterCollectionSavedAsync);

            AddPhoneNumberCommand = new DelegateCommand(OnAddPhoneNumberExecute);
            RemovePhoneNumberCommand = new DelegateCommand(OnRemovePhoneNumberExecute, OnRemovePhoneNumberCanExecute);

            Companies = new ObservableCollection<LookupItem>();
            PhoneNumbers = new ObservableCollection<ClientPhoneNumberWrapper>();
        }

        public override async Task LoadAsync(int clientId)
        {
            var client = clientId > 0
                ? await _clientRepository.GetByIdAsync(clientId)
                : CreateNewClient();

            Id = clientId;

            InitializeClient(client);

            InitializeClientPhoneNumbers(client.PhoneNumbers);

            await LoadCompaniesLookupAsync();
        }

        private void InitializeClientPhoneNumbers(ICollection<ClientPhoneNumber> phoneNumbers)
        {
            foreach (var wrapper in PhoneNumbers)
            {
                wrapper.PropertyChanged -= ClientPhoneNumberWrapper_PropertyChanged;
            }
            PhoneNumbers.Clear();

            foreach (var clientPhoneNumber in phoneNumbers)
            {
                var wrapper = new ClientPhoneNumberWrapper(clientPhoneNumber);
                PhoneNumbers.Add(wrapper);
                wrapper.PropertyChanged += ClientPhoneNumberWrapper_PropertyChanged;
            }
        }

        private void ClientPhoneNumberWrapper_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!HasChanges)
            {
                HasChanges = _clientRepository.HasChanges();
            }
            if (e.PropertyName == nameof(ClientPhoneNumberWrapper.HasErrors))
            {
                ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        private void InitializeClient(Client client)
        {
            Client = new ClientWrapper(client);
            Client.PropertyChanged += (s, e) =>
            {
                if (!HasChanges)
                {
                    HasChanges = _clientRepository.HasChanges(); // to be sure that we don`t call has changes method everytime, we only want to call if HasChanges is not true yet
                }
                if (e.PropertyName == nameof(Client.HasErrors))
                {
                    ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();
                }
                if (e.PropertyName == nameof(Client.FirstName)
                || e.PropertyName == nameof(Client.LastName))
                {
                    SetTitle();
                }

            };
            ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();

            if (Client.Id == 0)
            {
                // manipulation to trigger the validation
                Client.FirstName = "";
            }
            SetTitle();
        }

        private void SetTitle()
        {
            Title = $"{Client.FirstName} {Client.LastName}";
        }

        private async Task LoadCompaniesLookupAsync()
        {
            Companies.Clear();
            Companies.Add(new NullLookupItem { DisplayMember = " - " }); // to have possibility to display Null (or specific sign) in our combobox
            var lookup = await _companyLookupDataService.GetCompanyLookupAsync();

            foreach (var lookupItem in lookup)
            {
                Companies.Add(lookupItem);
            }
        }

        private Client CreateNewClient()
        {
            var client = new Client();
            _clientRepository.Add(client);

            return client;
        }

        protected override async void OnSaveExecute()
        {
            await SaveWithOptimisticConcurrencyAsync(_clientRepository.SaveAsync,
                () =>
                {
                    HasChanges = _clientRepository.HasChanges();
                    Id = Client.Id;
                    RaiseDetailSavedEvent(Client.Id, $"{Client.FirstName} {Client.LastName}");
                });
        }

        protected override bool OnSaveCanExecute()
        {
            return Client != null
                && !Client.HasErrors
                && PhoneNumbers.All(pn => !pn.HasErrors)
                && HasChanges;
        }

        protected override async void OnDeleteExecute()
        {
            if (await _clientRepository.HasMeetingsAsync(Client.Id))
            {
                await MessageDialogService.ShowInfoDialogAsync($"{Client.FirstName} {Client.LastName} can`t be deleted as this client is part of at least one meeting");
                return;
            }

            var result = await MessageDialogService.ShowOkCancelDialogAsync("Do you really want to delete the Client?",
                "Question");
            if (result == MessageDialogResult.Ok)
            {
                _clientRepository.Remove(Client.Model);
                await _clientRepository.SaveAsync();
                RaiseDetailDeletedEvent(Client.Id);
            }
        }

        private void OnAddPhoneNumberExecute()
        {
            var newNumber = new ClientPhoneNumberWrapper(new ClientPhoneNumber());
            newNumber.PropertyChanged += ClientPhoneNumberWrapper_PropertyChanged;
            PhoneNumbers.Add(newNumber);
            Client.Model.PhoneNumbers.Add(newNumber.Model);
            newNumber.Number = ""; //Trigger validation;
        }

        private void OnRemovePhoneNumberExecute()
        {
            SelectedPhoneNumber.PropertyChanged -= ClientPhoneNumberWrapper_PropertyChanged;
            _clientRepository.RemovePhoneNumber(SelectedPhoneNumber.Model);
            Client.Model.PhoneNumbers.Remove(SelectedPhoneNumber.Model);
            PhoneNumbers.Remove(SelectedPhoneNumber);
            SelectedPhoneNumber = null;
            HasChanges = _clientRepository.HasChanges();
            ((DelegateCommand)SaveCommand).RaiseCanExecuteChanged();
        }

        private bool OnRemovePhoneNumberCanExecute()
        {
            return SelectedPhoneNumber != null;
        }

        private async void AfterCollectionSavedAsync(AfterCollectionSavedEventArgs args)
        {
            if (args.ViewModelName == nameof(CompanyDetailViewModel))
            {
                await LoadCompaniesLookupAsync();
            }
        }

    }
}
