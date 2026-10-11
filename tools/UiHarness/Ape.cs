using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void ApeChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        var vm = Open<AnnualPhysicalExamViewModel>(ModuleIds.AnnualPhysicalExam);
        Pump(1000);
        Check("lists load", vm.HistoryRows.Count == 6 && vm.Ent.EditCommand is not null && vm.VitalSignsBy.EditCommand is not null, "");
        Snapshot("ape-1-new.png");

        Act(() => vm.FindText = "Emma", 1500);
        var pick = vm.Suggestions.First();
        Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("a registration fills the patient block", vm.HasRegistration && vm.PatientName.Length > 0, vm.PatientName);

        vm.DepartmentOrAgency = "ACCOUNTING DEPT";
        vm.BirthDate = new DateTime(1990, 5, 17);
        vm.CivilStatus = "Married";
        vm.ContactNo = "0917 123 4567";
        vm.Ent.Value = "ENT-X"; vm.Gastroenterology.Value = "GASTRO-X"; vm.Respiratory.Value = "RESP-X"; vm.IntegumentarySkin.Value = "SKIN-X";
        vm.Cardiology.Value = "CARDIO-X"; vm.Psychology.Value = "PSYCH-X"; vm.Endocrinology.Value = "ENDO-X"; vm.ObGyneUrology.Value = "OB-X";
        vm.Musculoskeletal.Value = "MUSC-X"; vm.Infectious.Value = "INFECT-X"; vm.Neurological.Value = "NEURO-X"; vm.Surgical.Value = "SURG-X";
        vm.OthersPast = "OTHERS PAST XXX"; vm.Medications = "MEDS XXX"; vm.Allergies = "ALLERGY XXX"; vm.ReviewOfSystems = "ROS XXX";
        vm.Smoking.Value = "Yes"; vm.SmokingSinceWhen = "2010"; vm.SticksPerDay = "5";
        vm.Drinking.Value = "Yes"; vm.DrinkingSinceWhen = "2012"; vm.Bottles = "2"; vm.DrinkingFrequency.Value = "Weekly";
        vm.Lmp = "04/01/2026"; vm.LmpType.Value = "Regular";
        vm.Bp1st = "120/80"; vm.Bp2nd = "118/78"; vm.CardiacRate1st = "72"; vm.CardiacRate2nd = "70";
        vm.Height = "5.6"; vm.Weight = "62"; vm.BmiCategory = "NORMAL BMI";
        vm.VaRightWith = "20/20"; vm.VaLeftWith = "20/25"; vm.VaRightWithout = "20/30"; vm.VaLeftWithout = "20/40"; vm.VisualAcuity.Value = "EOR";
        for (var i = 0; i < vm.Exam.Count; i++)
            vm.Exam[i].State.Value = i % 4 == 0 ? "F" : "N";
        vm.Findings = "FINDINGS XXX";
        vm.VitalSignsBy.Value = "NURSE ANNA"; vm.HeightWeightBy.Value = "NURSE BEN";
        Pump(400);
        Snapshot("ape-2-filled.png");
        _main.Height = 2300;
        Pump(800);
        Snapshot("ape-2-tall.png");
        _main.Height = 728;
        Pump(400);

        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saved", vm.Id != 0 && !vm.IsError, vm.Message);
        var id = vm.Id;

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Check("new form is empty", vm.Smoking.Value is null && vm.Exam.All(r => r.State.Value is null) && vm.Ent.Value is null, "");
        Act(() => vm.SearchText = "Emma");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == id), 1800);
        Check("reopening restores the fields", vm.Ent.Value == "ENT-X" && vm.Smoking.Value == "Yes" && vm.SticksPerDay == "5" && vm.DrinkingFrequency.Value == "Weekly"
            && vm.VisualAcuity.Value == "EOR" && vm.Exam[0].State.Value == "F" && vm.Exam[1].State.Value == "N" && vm.BirthDate == new DateTime(1990, 5, 17)
            && vm.Findings == "FINDINGS XXX" && vm.HeightWeightBy.Value == "NURSE BEN", $"{vm.Ent.Value}/{vm.Smoking.Value}/{vm.SticksPerDay}/{vm.DrinkingFrequency.Value}/{vm.VisualAcuity.Value}/{vm.Exam[0].State.Value}/{vm.Exam[1].State.Value}/{vm.BirthDate:o}/{vm.Findings}/{vm.HeightWeightBy.Value}");

        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the APE layout", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.AnnualPhysicalExam } r && r.Fields is not null, CapturePreview.Last?.Layout.ToString() ?? "none");

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        vm.Exam[0].State.Value = "N"; vm.Exam[1].State.Value = "N"; vm.Findings = "DEFAULT FINDINGS"; vm.Ent.Value = "NONE";
        Act(() => vm.SaveDefaultsCommand.Execute(null), 2500);
        Check("defaults cover exam marks, texts and lists", vm.Exam[0].State.Value == "N" && vm.Findings == "DEFAULT FINDINGS" && vm.Ent.Value == "NONE", "");
        vm.Exam[0].State.Value = "F";
        Act(() => vm.ResetToDefaultsCommand.Execute(null), 300);
        Check("reset restores them", vm.Exam[0].State.Value == "N", vm.Exam[0].State.Value ?? "");
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        Act(() => vm.ClearDefaultsCommand.Execute(null), 2500);
        Check("defaults can be cleared", vm.Exam[0].State.Value is null && vm.Findings is null, "");

        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Id == id) ?? vm.Items.First(), 1800);
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }
}
