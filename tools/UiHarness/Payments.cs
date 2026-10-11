using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;
using System.Windows;

namespace UiHarness;

internal static partial class Program
{
    private static void PaymentChecks(IServiceProvider provider)
    {
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        // a maintained discount with two options
        var discounts = Open<DiscountsViewModel>(ModuleIds.Discounts);
        Act(() => discounts.NewRecordCommand.Execute(null));
        discounts.DiscountName = "PWD Harness";
        discounts.DiscountDescription = "d";
        Act(() => discounts.AddLineCommand.Execute(null));
        discounts.Lines[0].Value = 20m;
        Act(() => discounts.AddLineCommand.Execute(null));
        discounts.Lines[1].IsPercentage = false;
        discounts.Lines[1].Value = 50m;
        Act(() => discounts.SaveCommand.Execute(null), 2500);
        Check("discount seeded", discounts.Id != 0 && !discounts.IsError, discounts.Message);

        // registrations to pay
        var codes = new List<string>();
        var reg = Open<RegistrationsViewModel>(ModuleIds.PatientRegistrations);
        Pump(1000);
        var (a, b) = (reg.ServiceOptions[0], reg.ServiceOptions[1]);
        foreach (var name in new[] { "Pay Tester", "Charge Tester", "Discount Tester" })
        {
            Act(() => reg.NewRecordCommand.Execute(null), 1200);
            reg.PatientName = name;
            ScriptedPicker.Answer = (_, _) => [a.Id, b.Id];
            Act(() => reg.ChooseServicesCommand.Execute(null));
            reg.AmountDue = 1000m;
            Act(() => reg.SaveCommand.Execute(null), 3000);
            Check($"registration for {name} created", reg.Id != 0 && !reg.IsError, reg.Message);
            codes.Add(reg.RegistrationCode);
        }

        var vm = Open<PaymentsViewModel>(ModuleIds.Payments);
        Pump(1000);
        Check("nothing is loaded at first and the payment controls are off", !vm.HasRegistration && vm.PaymentAmount == 0m, "");
        Snapshot("pay-1-empty.png");

        vm.FindText = "nothing-like-this";
        Act(() => vm.FindCommand.Execute(null));
        Check("an unknown code is reported", vm.IsError && vm.Message.Contains("No registration"), vm.Message);

        vm.FindText = codes[0];
        Act(() => vm.FindCommand.Execute(null));
        Check("the registration is found and its services shown", vm.HasRegistration && vm.Registration!.PatientName == "Pay Tester" && vm.ServiceLines.Count == 2, vm.Message);
        Check("the amount is pre-filled with the balance", vm.PaymentAmount == 1000m && vm.BalanceBefore == 1000m && vm.BalanceAfter == 0m, $"{vm.PaymentAmount}");

        vm.DiscountIsPercentage = true;
        vm.DiscountValue = 10m;
        Check("a percentage discount lowers the amount due and the amount follows", vm.DiscountTotal == 100m && vm.AmountDue == 900m && vm.PaymentAmount == 900m, $"{vm.DiscountTotal}/{vm.AmountDue}/{vm.PaymentAmount}");

        vm.PaymentAmount = 400m;
        Check("typing an amount shows the balance left", vm.BalanceAfter == 500m, $"{vm.BalanceAfter}");
        Snapshot("pay-2-filled.png");
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("the part payment is saved", vm.Id != 0 && !vm.IsError, vm.Message);
        Check("the form shows what is paid and left, without double counting this payment", vm.Paid == 0m && vm.BalanceBefore == 900m && vm.BalanceAfter == 500m, $"{vm.Paid}/{vm.BalanceBefore}/{vm.BalanceAfter}");
        Snapshot("pay-3-saved.png");

        // pay too much
        Act(() => vm.SaveAndNewCommand.Execute(null), 3000);
        Check("Save & New with an unchanged saved payment starts a new form", vm.IsNew && !vm.HasRegistration, vm.Message);
        vm.FindText = codes[0];
        Act(() => vm.FindCommand.Execute(null));
        Check("the next payment starts at the remaining balance and keeps the discount", vm.PaymentAmount == 500m && vm.Paid == 400m && vm.DiscountTotal == 100m, $"{vm.PaymentAmount}/{vm.Paid}/{vm.DiscountTotal}");
        vm.PaymentAmount = 600m;
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("paying more than the balance is refused", vm.IsError && vm.Message.Contains("balance of 500.00"), vm.Message);
        Act(() => vm.PayBalanceCommand.Execute(null));
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("Pay balance settles it", !vm.IsError && vm.BalanceAfter == 0m, vm.Message);
        Act(() => vm.NewRecordCommand.Execute(null));

        // a typed name offers the registration
        Act(() => vm.FindText = "Charge Te", 1500);
        Check("typing offers matching registrations with their balance", vm.Suggestions.Count == 1 && vm.Suggestions[0].Balance == 1000m, $"{vm.Suggestions.Count}");
        var pick = vm.Suggestions[0];
        Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("picking one loads it", vm.HasRegistration && vm.Registration!.RegistrationCode == codes[1] && !vm.HasSuggestions, "");

        // charge
        Act(() => vm.IsCharge = true, 500);
        Check("charging clears the amount and the balance", vm.PaymentAmount == 0m && vm.BalanceAfter == 0m && !vm.CanEditAmount, $"{vm.PaymentAmount}");
        Snapshot("pay-4-charge.png");
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("the charge is saved", vm.Id != 0 && !vm.IsError && vm.Message.Contains("charged"), vm.Message);
        var chargeId = vm.Id;
        Check("a charged registration says so", vm.Registration!.IsCharged || vm.IsCharge, vm.RegistrationNote);

        // maintained discount
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.FindText = codes[2];
        Act(() => vm.FindCommand.Execute(null));
        var pwd = vm.Discounts.First(d => d.Name == "PWD Harness");
        Check("the maintained discount is offered", vm.Discounts.Count >= 2 && pwd.Choices.Count == 2 && vm.CanTypeDiscount, string.Join(", ", vm.Discounts.Select(d => d.Name)));
        Act(() => vm.SelectedDiscountId = pwd.Id, 800);
        Check("choosing it lists all its details in order and applies them one after another", vm.HasDiscountSteps && vm.DiscountSteps.Count == 2 && vm.DiscountSteps[0].Cut == 100m && vm.DiscountSteps[1].Cut == 50m && !vm.CanTypeDiscount && vm.DiscountTotal == 150m && vm.AmountDue == 850m && vm.PaymentAmount == 850m, $"{vm.DiscountSteps.Count} {vm.DiscountTotal} {vm.AmountDue} {vm.PaymentAmount}");
        Snapshot("pay-7-discount.png");
        vm.PaymentAmount = 300m;
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saved with the discount", vm.Id != 0 && !vm.IsError, vm.Message);
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.FindText = codes[2];
        Act(() => vm.FindCommand.Execute(null));
        Check("the discount is remembered on the registration with its details", vm.SelectedDiscountId == pwd.Id && vm.DiscountSteps.Count == 2 && vm.DiscountTotal == 150m && !vm.CanTypeDiscount, $"{vm.SelectedDiscountId} {vm.DiscountSteps.Count} {vm.DiscountTotal}");        Act(() => vm.SelectedDiscountId = 0, 500);
        Check("choosing none unlocks the value", vm.CanTypeDiscount, "");
        Act(() => vm.NewRecordCommand.Execute(null));

        // list, filters, reopen        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.SearchText = string.Empty);
        Act(() => vm.SearchCommand.Execute(null));
        Check("payments are listed", vm.Items.Count >= 4, $"{vm.Items.Count} items");
        Snapshot("pay-5-list.png");
        Act(() => vm.DateFilter = DateTime.Today.AddDays(-30), 1500);
        Check("the date filter narrows the list", vm.Items.Count == 0, $"{vm.Items.Count}");
        Act(() => vm.ClearFiltersCommand.Execute(null), 1500);

        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id != chargeId && i.Amount == 400m), 1500);
        Check("opening a saved payment shows its registration, discount and amount", vm.HasRegistration && vm.PaymentAmount == 400m && vm.DiscountIsPercentage && vm.DiscountValue == 10m && !vm.CanChangeRegistration, $"{vm.PaymentAmount}/{vm.DiscountValue}");
        vm.PaymentAmount = 300m;
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("a saved payment can be edited", !vm.IsError && vm.PaymentAmount == 300m, vm.Message);
        Snapshot("pay-6-edited.png");
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("a payment can be deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }
}
