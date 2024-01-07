﻿using System.Collections.ObjectModel;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class FaqService : IFaqService
{
    private const string JsonName = "FAQ.json";
    private DateTime _lastUpdateDateTime;
    private readonly TimeSpan _minDateTime = new(1, 0, 0, 0);
    private ObservableCollection<Faq> _faqs = new();
    private readonly ILocalSettingsService _localSettingsService;
    private bool _isInitialized;
    public bool IsUpdating;
    public event VoidDelegate? UpdateStatusChangeEvent;

    public FaqService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    private async Task Init()
    {
        _lastUpdateDateTime = _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.FaqLastUpdate).Result;
        // 从本地文件读取
        await LoadFaqs();
        _isInitialized = true;
    }

    public async Task<ObservableCollection<Faq>> GetFaqAsync(bool forceRefresh = false)
    {
        if (!_isInitialized)
            await Init();
        
        if (!forceRefresh && DateTime.Now - _lastUpdateDateTime < _minDateTime || IsUpdating)
            return _faqs;
        
        IsUpdating = true;
        UpdateStatusChangeEvent?.Invoke();
        var local = ResourceExtensions.GetLocal();
        await DownloadAndSaveFaqs($"https://raw.gitmirror.com/GoldenPotato137/GalgameManager/main/docs/FAQ/{local}.json");
        await LoadFaqs();
        _lastUpdateDateTime = DateTime.Now;
        await _localSettingsService.SaveSettingAsync(KeyValues.FaqLastUpdate, _lastUpdateDateTime);
        
        IsUpdating = false;
        UpdateStatusChangeEvent?.Invoke();
        return _faqs;
    }

    private static async Task DownloadAndSaveFaqs(string? jsonUrl)
    {
        if (jsonUrl == null) return;
        HttpClient httpClient = new();
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(jsonUrl);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsByteArrayAsync();
            StorageFolder? localFolder = ApplicationData.Current.LocalFolder;
            StorageFile? storageFile =
                await localFolder.CreateFileAsync(JsonName, CreationCollisionOption.ReplaceExisting);
            Stream? fileStream = await storageFile.OpenStreamForWriteAsync();
            MemoryStream memoryStream = new(data);
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(fileStream);
            fileStream.Close();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private async Task LoadFaqs()
    {
        try
        {
            StorageFolder? localFolder = ApplicationData.Current.LocalFolder;
            StorageFile? storageFile = await localFolder.TryGetItemAsync(JsonName) as StorageFile;
            if (storageFile != null)
            {
                var json = await FileIO.ReadTextAsync(storageFile);
                if (json != null)
                {
                    _faqs.Clear();
                    _faqs = JsonConvert.DeserializeObject<ObservableCollection<Faq>>(json) ??
                            new ObservableCollection<Faq>();
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
}