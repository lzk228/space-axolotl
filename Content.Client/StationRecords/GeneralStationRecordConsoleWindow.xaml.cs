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

    private bool _isPopulating;

    private string _fingerPrintsFilter = "";

    private GeneralStationRecordConsoleState? _state;

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

        RecordListingFingerPrintsFilter.OnPressed += _ =>
        {
            string fingerPrints = RecordListinFingerPrintsLine.Text;
            if (fingerPrints.Length > 0 && fingerPrints != _fingerPrintsFilter)
            {
                FilterRecordListingForFingerPrints(fingerPrints);
            }
        };

        RecordListingFingerPrintsReset.OnPressed += _ =>
        {
            RecordListinFingerPrintsLine.Text = "";
            FilterRecordListingForFingerPrints("");
        };
    }

    public void UpdateState(GeneralStationRecordConsoleState state, bool setState = true)
    {
        if (state.RecordListing == null)
        {
            RecordListingStatus.Visible = true;
            RecordListing.Visible = false;
            RecordListingStatus.Text = Loc.GetString("general-station-record-console-empty-state");
            return;
        }

        if (setState)
        {
            _state = state;
        }

        RecordListingStatus.Visible = false;
        RecordListing.Visible = true;

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

    private void PopulateRecordListing(Dictionary<StationRecordKey, RecordListingValue> listing, StationRecordKey? selected)
    {
        RecordListing.Clear();
        RecordListing.ClearSelected();

        _isPopulating = true;

        Logger.Debug($"filter for fingerPrints ++++++ {_fingerPrintsFilter}");

        foreach ((StationRecordKey key, RecordListingValue value) in listing)
        {
            if (_fingerPrintsFilter.Length > 0 && !CheckFingerPrint(value.fingerPrint)) {
                continue;
            }

            string personalName = value.name;

            var item = RecordListing.AddItem(personalName);
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
                Text = Loc.GetString("general-station-record-console-record-fingerprint", ("fingerprint", record.Fingerprint is null ? Loc.GetString("generic-not-available-shorthand") : record.Fingerprint))
            }
        };

        foreach (var control in recordControls)
        {
            RecordContainer.AddChild(control);
        }
    }
    private void FilterRecordListingForFingerPrints(string fingerPrints)
    {
        if (_state != null)
        {
            _fingerPrintsFilter = fingerPrints.ToLower();
            UpdateState(_state, false);
        }
    }
    private bool CheckFingerPrint(string print = "") {
        string lowerCasePrints = print.ToLower();
        Logger.Debug($"print {lowerCasePrints}, savePrint {_fingerPrintsFilter}");
        return lowerCasePrints.StartsWith(_fingerPrintsFilter);
    }
}
