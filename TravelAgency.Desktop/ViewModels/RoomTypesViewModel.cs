using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TravelAgency.Data;
using TravelAgency.Domain.Entities;

namespace TravelAgency.Desktop.ViewModels
{
    public partial class RoomTypesViewModel : ObservableObject
    {
        private readonly TravelAgencyDbContext _db;
        public ObservableCollection<RoomType> RoomTypes { get; } = new();

        [ObservableProperty] 
        private RoomType? selected;
        [ObservableProperty] 
        private string? searchText;
        [ObservableProperty] 
        private string? editCode;
        [ObservableProperty] 
        private string? editName;

        public RoomTypesViewModel(TravelAgencyDbContext db) => _db = db;

        [RelayCommand]
        private async Task LoadAsync()
        {
            RoomTypes.Clear();
            var q = _db.RoomTypes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchText))
                q = q.Where(x => x.Code.Contains(SearchText) || x.Name.Contains(SearchText));
            foreach (var it in await q.OrderBy(x=>x.Code).AsNoTracking().ToListAsync())
                RoomTypes.Add(it);
        }

        partial void OnSelectedChanged(RoomType? value)
        {
            EditCode = value?.Code;
            EditName = value?.Name;
        }

        [RelayCommand] private void New()
        {
            Selected = null;
            EditCode = string.Empty;
            EditName = string.Empty;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName)) return;

            if (Selected == null)
            {
                _db.RoomTypes.Add(new RoomType { Code = EditCode!.Trim(), Name = EditName!.Trim() });
            }
            else
            {
                var entity = await _db.RoomTypes.FirstAsync(x => x.Id == Selected.Id);
                entity.Code = EditCode!.Trim();
                entity.Name = EditName!.Trim();
            }

            await _db.SaveChangesAsync();
            await LoadAsync();
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            if (Selected == null) return;
            var entity = await _db.RoomTypes.FirstAsync(x => x.Id == Selected.Id);
            _db.RoomTypes.Remove(entity);
            await _db.SaveChangesAsync();
            await LoadAsync();
        }
    }
}
