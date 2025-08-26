using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class RoomTypesViewModel : ObservableObject
    {
        private readonly IDbContextFactory<TravelAgencyDbContext> _dbf;
        public RoomTypesViewModel(IDbContextFactory<TravelAgencyDbContext> dbf) => _dbf = dbf;

        public ObservableCollection<RoomType> Items { get; } = new();

        [ObservableProperty] private RoomType? selected;
        [ObservableProperty] private string? searchText;

        // editor state
        [ObservableProperty] private bool isEditing;
        [ObservableProperty] private string editorTitle = "Select a row and click Edit, or click Add New";
        [ObservableProperty] private string editorHint = "Use the left list to select an item for editing.";

        // editor fields (decoupled from Selected)
        [ObservableProperty] private string? editCode;
        [ObservableProperty] private string? editName;

        // mode flags
        private bool _isNewMode;
        private int? _editingId;
         

        public bool CanEdit => Selected != null && !IsEditing;
        public bool CanDelete => Selected != null && !IsEditing;

        partial void OnSelectedChanged(RoomType? value)
        {
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            Items.Clear();
            var q = db.RoomTypes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(x => x.Code.Contains(SearchText) || x.Name.Contains(SearchText));
            foreach (var it in await q.OrderBy(x => x.Code).AsNoTracking().ToListAsync())
                Items.Add(it);

            if (!IsEditing)
            {
                EditorTitle = "Select a row and click Edit, or click Add New";
                EditorHint = "Use the left list to select an item for editing.";
            }
        }

        [RelayCommand]
        private void BeginNew()
        {
            _isNewMode = true;
            _editingId = null;
            IsEditing = true;
            EditCode = string.Empty;
            EditName = string.Empty;
            EditorTitle = "Add New Room Type";
            EditorHint = "Fill the fields and click Save.";
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (Selected == null) return;
            _isNewMode = false;
            _editingId = Selected.Id;
            IsEditing = true;
            EditCode = Selected.Code;
            EditName = Selected.Name;
            EditorTitle = $"Edit Room Type #{Selected.Id}";
            EditorHint = "Change values and click Save.";
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName)) return;

            if (_isNewMode)
            {
                db.RoomTypes.Add(new RoomType { Code = EditCode!.Trim(), Name = EditName!.Trim() });
            }
            else if (_editingId.HasValue)
            {
                var entity = await db.RoomTypes.FirstAsync(x => x.Id == _editingId.Value);
                entity.Code = EditCode!.Trim();
                entity.Name = EditName!.Trim();
            }

            await db.SaveChangesAsync();
            IsEditing = false;
            await LoadAsync();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditing = false;
            _isNewMode = false;
            _editingId = null;
            EditorTitle = "Select a row and click Edit, or click Add New";
            EditorHint = "Use the left list to select an item for editing.";
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            await using var db = await _dbf.CreateDbContextAsync();

            if (Selected == null) return;
            var entity = await db.RoomTypes.FirstAsync(x => x.Id == Selected.Id);
            db.RoomTypes.Remove(entity);
            await db.SaveChangesAsync();
            await LoadAsync();
        }
    }
}
