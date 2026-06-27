using Microsoft.EntityFrameworkCore;
using TelegramCarsBot.Data;
using TelegramCarsBot.Models;

namespace TelegramCarsBot.Services;

public class CarService
{
    private readonly AppDbContext _db;

    public CarService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Car> AddAsync(Car car)
    {
        car.CreatedDate = DateTime.UtcNow;
        car.UpdatedDate = DateTime.UtcNow;
        _db.Cars.Add(car);
        await _db.SaveChangesAsync();
        return car;
    }

    public async Task<List<Car>> GetAllAsync()
        => await _db.Cars.OrderByDescending(c => c.CreatedDate).ToListAsync();

    public async Task<Car?> GetByIdAsync(int id)
        => await _db.Cars.FindAsync(id);

    public async Task<Car?> UpdateAsync(int id, Action<Car> updateAction)
    {
        var car = await _db.Cars.FindAsync(id);
        if (car is null) return null;

        updateAction(car);
        car.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return car;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var car = await _db.Cars.FindAsync(id);
        if (car is null) return false;

        _db.Cars.Remove(car);
        await _db.SaveChangesAsync();
        return true;
    }
}
