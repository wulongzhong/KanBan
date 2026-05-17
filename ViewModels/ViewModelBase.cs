using System;
using CommunityToolkit.Mvvm.ComponentModel;
using KanBan.Services.Localization;

namespace KanBan.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected void SubscribeLocalization(Action refresh)
    {
        LocalizationService.Instance.PropertyChanged += (_, _) => refresh();
    }
}
