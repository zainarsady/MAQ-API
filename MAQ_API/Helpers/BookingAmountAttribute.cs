using System.ComponentModel.DataAnnotations;
using MAQ_API.Services;

namespace MAQ_API.Helpers;

/// <summary>TEMPORARY: also allows the RM2 test amount (<see cref="TempahanRamadanService.TestAmount"/>). Amount must be a whole number of slots: a multiple of the slot price, 1 to 20 slots.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class BookingAmountAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is decimal amount
        && (amount == TempahanRamadanService.TestAmount
        || amount >= TempahanRamadanService.SlotPrice
        && amount <= TempahanRamadanService.SlotPrice * TempahanRamadanService.MaxBookingsPerDay
        && amount % TempahanRamadanService.SlotPrice == 0);

    public override string FormatErrorMessage(string name) =>
        $"{name} must be a multiple of RM{TempahanRamadanService.SlotPrice} " +
        $"between RM{TempahanRamadanService.SlotPrice} and RM{TempahanRamadanService.SlotPrice * TempahanRamadanService.MaxBookingsPerDay} (1-{TempahanRamadanService.MaxBookingsPerDay} slots), or RM{TempahanRamadanService.TestAmount} for testing.";
}
