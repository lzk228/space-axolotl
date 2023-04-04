using System.Linq;
using Content.Shared.StationRecords;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.StationRecords;

[GenerateTypedNameReferences]
public sealed partial class GeneralStationRecordConsoleWindow : DefaultWindow
{
    public Action<StationRecordKey?>? OnKeySelected;

    public Action<GeneralStationRecordFilterType, string>? OnFiltersChanged;

    private bool _isPopulating;

    private int _currentFilterType = 0;

    private GeneralStationRecordFilterType[] _filterTypes
        = Enum.GetValues<GeneralStationRecordFilterType>();

    public GeneralStationRecordConsoleWindow()
    {
        RobustXamlLoader.Load(this);

        RecordListing.OnItemSelected += args =>
        {
            if (_isPopulating || RecordListing[args.ItemIndex].Metadata is not StationRecordKey cast)
            {
                return;
            }

            OnKeySelected?.Invoke(cast);
        };

        RecordListing.OnItemDeselected += _ =>
        {
            if (!_isPopulating)
                OnKeySelected?.Invoke(null);
        };

        for (int i = 0; i < _filterTypes.Length; i++)
        {
            StationRecordsFilterType.AddItem(GetTypeFilterLocals(i));
        }

        StationRecordsFilterType.OnItemSelected += eventArgs =>
        {
            if (_currentFilterType != eventArgs.Id)
            {
                _currentFilterType = eventArgs.Id;
                FilterListingOfRecords();
            }
        };

        StationRecordsFiltersValue.OnTextEntered += args =>
        {
            FilterListingOfRecords(args.Text);
        };

        StationRecordsFilters.OnPressed += _ =>
        {
            FilterListingOfRecords(StationRecordsFiltersValue.Text);
        };

        StationRecordsFiltersReset.OnPressed += _ =>
        {
            StationRecordsFiltersValue.Text = "";
            FilterListingOfRecords();
        };
    }

    public void UpdateState(GeneralStationRecordConsoleState state)
    {
        if (state.Filter != null)
        {
            if (state.Filter.type != _filterTypes[_currentFilterType])
            {
                int findedIndex = Array.IndexOf(_filterTypes, state.Filter.type);
                _currentFilterType = findedIndex > 0 ? findedIndex : 0;
            }

            if (state.Filter.value != StationRecordsFiltersValue.Text)
            {
                StationRecordsFiltersValue.Text = state.Filter.value;
            }
        }

        StationRecordsFilterType.SelectId(_currentFilterType);
        StationRecordsFiltersValue.PlaceHolder = GetTypeFilterLocals(_currentFilterType, false);

        if (state.RecordListing == null)
        {
            RecordListingStatus.Visible = true;
            RecordListing.Visible = false;
            RecordListingStatus.Text = Loc.GetString("general-station-record-console-empty-state");
            RecordContainer.Visible = false;
            RecordContainerStatus.Visible = false;
            return;
        }

        RecordListingStatus.Visible = false;
        RecordListing.Visible = true;
        RecordContainer.Visible = true;

        PopulateRecordListing(state.RecordListing!, state.SelectedKey);

        RecordContainerStatus.Visible = state.Record == null;

        if (state.Record != null)
        {
            RecordContainerStatus.Visible = state.SelectedKey == null;
            RecordContainerStatus.Text = state.SelectedKey == null
                ? Loc.GetString("general-station-record-console-no-record-found")
                : Loc.GetString("general-station-record-console-select-record-info");
            PopulateRecordContainer(state.Record);
        }
        else
        {
            RecordContainer.DisposeAllChildren();
            RecordContainer.RemoveAllChildren();
        }
    }
    private void PopulateRecordListing(Dictionary<StationRecordKey, string> listing, StationRecordKey? selected)
    {
        RecordListing.Clear();
        RecordListing.ClearSelected();

        _isPopulating = true;

        foreach (var (key, name) in listing)
        {
            var item = RecordListing.AddItem(name);
            item.Metadata = key;
            if (selected != null && key.ID == selected.Value.ID)
            {
                item.Selected = true;
            }
        }
        _isPopulating = false;

        RecordListing.SortItemsByText();
    }

    private void PopulateRecordContainer(GeneralStationRecord record)
    {
        RecordContainer.DisposeAllChildren();
        RecordContainer.RemoveAllChildren();
        // sure
        var recordControls = new Control[]
        {
            new Label()
            {
                Text = record.Name,
                StyleClasses = { "LabelBig" }
            },
            new Label()
            {
                Text = Loc.GetString("general-station-record-console-record-age", ("age", record.Age.ToString()))

            },
            new Label()
            {
                Text = Loc.GetString("general-station-record-console-record-title", ("job", Loc.GetString(record.JobTitle)))
            },
            new Label()
            {
                Text = Loc.GetString("general-station-record-console-record-species", ("species", record.Species))
            },
            new Label()
            {
                Text = Loc.GetString("general-station-record-console-record-gender", ("gender", record.Gender.ToString()))
            },
            new Label()
            {
                Text = Loc.GetString("general-station-record-console-record-fingerprint", ("fingerprint", record.Fingerprint ?? Loc.GetString("generic-not-available-shorthand")))
            },
            new Label()
            {
                Text = Loc.GetString("general-station-record-console-record-dna", ("dna", record.DNA ?? Loc.GetString("generic-not-available-shorthand")))
            }
        };

        foreach (var control in recordControls)
        {
            RecordContainer.AddChild(control);
        }
    }

    private void FilterListingOfRecords(string text = "")
    {
        if (!_isPopulating)
        {
            OnFiltersChanged?.Invoke(_filterTypes[_currentFilterType], text);
        }
    }

    private string GetTypeFilterLocals(int id, bool isForButton = true)
    {
        string filterType = _filterTypes[id].ToString().ToLower();

        if (isForButton)
        {
            return Loc.GetString($"general-station-record-{filterType}-filter");
        }
        else
        {
            return Loc.GetString($"general-station-record-for-{filterType}-placeholder");
        }
    }
}
