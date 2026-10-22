using System;
using System.Collections.Generic;

namespace UniversalSqueaker.OneShot;

public static class EatConditions
{
    public static Condition Restriction(EatPhase phase, EatNutritionScope nutrition, EatDrugScope drug)
    {
        if (!Enum.IsDefined(typeof(EatPhase), phase) || !Enum.IsDefined(typeof(EatNutritionScope), nutrition)
            || !Enum.IsDefined(typeof(EatDrugScope), drug)) throw new ArgumentException("Invalid explicit Eat parameter");
        var clauses = new List<Condition>();
        if (phase == EatPhase.Execution) clauses.Add(new Condition("ref", EatPolicy.Execution));
        if (nutrition != EatNutritionScope.Any)
            clauses.Add(nutrition == EatNutritionScope.NutritionGiving ? new Condition("ref", EatPolicy.Nutrition)
                : new Condition("not", "", new Condition("ref", EatPolicy.Nutrition)));
        if (drug != EatDrugScope.Any)
            clauses.Add(drug == EatDrugScope.OnlyDrugs ? new Condition("ref", EatPolicy.Drug)
                : new Condition("not", "", new Condition("ref", EatPolicy.Drug)));
        return new Condition("all", "", clauses.ToArray());
    }
}
