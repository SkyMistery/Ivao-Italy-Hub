using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The four declarations the start-up check refuses besides A3b's two (note 2026-09-30-il-controllo-all-avvio-rinforzato): each
/// one would let the single handler and the interceptor's guard give two different answers on the same row, the endpoint "yes"
/// and the save a 403 nobody can explain.
/// <list type="bullet">
/// <item>a marked permission whose <c>Edit</c> is not the one of the entity's area — the handler falls back on the first, the
/// guard on the second;</item>
/// <item>a marked <c>Edit</c> — it would be its own fallback, and narrow to the assignee whoever writes the whole area;</item>
/// <item>a marked alternative on an entity that has no department — the guard asks the alternative on the row's departments;</item>
/// <item>a marked permission not denied to the stakeholder on an entity that has one — the guard keeps the stakeholder out of every
/// alternative, the handler only out of the permissions marked so.</item>
/// </list>
/// </summary>
public sealed class VerifyAlternativesTests
{
    private const string Area = "Probe";
    private const string View = "Probe.View";
    private const string Edit = "Probe.Edit";
    private const string Manage = "Probe.Manage";
    private const string Mark = "Probe.Mark";
    private const string Decide = "Probe.Decide";
    private const string OtherEdit = "Other.Edit";
    private const string OtherManage = "Other.Manage";

    private static readonly PermissionCatalog Catalogue = new([
        new PermissionDescriptor(View, IsGlobal: false),
        new PermissionDescriptor(Edit, IsGlobal: false),
        new PermissionDescriptor(Manage, IsGlobal: false, DeniedToStakeholder: true, OnlyForAssignee: true),
        new PermissionDescriptor(Mark, IsGlobal: false, OnlyForAssignee: true),
        new PermissionDescriptor(Decide, IsGlobal: false),
        new PermissionDescriptor(OtherEdit, IsGlobal: false),
        new PermissionDescriptor(OtherManage, IsGlobal: false, DeniedToStakeholder: true, OnlyForAssignee: true),
    ]);

    [PermissionArea(Area)]
    [AlsoWrittenWith(OtherManage)]
    private sealed class MarkedWithAnotherAreasPermission : IOwnedByDepartment, IHasAssignee
    {
        public Department OwnerDepartment => Department.TD;

        public int? AssigneeVid => null;
    }

    // No [PermissionArea]: the area is the one the hub works out from the context, as the guard does.
    [AlsoWrittenWith(Manage)]
    private sealed class ExposedAsAnotherSet : IOwnedByDepartment, IHasAssignee
    {
        public Department OwnerDepartment => Department.TD;

        public int? AssigneeVid => null;
    }

    [PermissionArea(Area)]
    [AlsoWrittenWith(Manage)]
    private sealed class MarkedWithNoDepartment : IHasAssignee
    {
        public int? AssigneeVid => null;
    }

    [PermissionArea(Area)]
    [AlsoWrittenWith(Mark)]
    private sealed class MarkedButOpenToTheStakeholder : IOwnedByDepartment, IHasAssignee, IHasStakeholder
    {
        public Department OwnerDepartment => Department.TD;

        public int? AssigneeVid => null;

        public int? StakeholderVid => null;
    }

    [PermissionArea(Area)]
    [AlsoWrittenWith(Manage, AlsoOnCreation = true, AlsoOnDeletion = true)]
    [AlsoWrittenWith(Decide)]
    private sealed class WrittenAsTheRulesSay : IOwnedByDepartment, IHasAssignee, IHasStakeholder
    {
        public Department OwnerDepartment => Department.TD;

        public int? AssigneeVid => null;

        public int? StakeholderVid => null;
    }

    // An alternative that is not marked asks nothing of the entity: the PIREP, a tour's child, has no department of its own.
    [PermissionArea(Area)]
    [AlsoWrittenWith(Decide)]
    private sealed class UnmarkedWithNoDepartment : IHasStakeholder
    {
        public int? StakeholderVid => null;
    }

    [Fact]
    public void AMarkedPermissionOfAnotherAreaIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => Catalogue.VerifyAlternatives([typeof(MarkedWithAnotherAreasPermission)]));

        Assert.Contains(nameof(MarkedWithAnotherAreasPermission), refused.Message, StringComparison.Ordinal);
        Assert.Contains(OtherManage, refused.Message, StringComparison.Ordinal);
        Assert.Contains(Edit, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAreaIsTheOneTheHubWorksOutForTheEntity()
    {
        Assert.Null(Record.Exception(() => Catalogue.VerifyAlternatives([typeof(ExposedAsAnotherSet)], _ => Area)));

        var refused = Assert.Throws<InvalidOperationException>(
            () => Catalogue.VerifyAlternatives([typeof(ExposedAsAnotherSet)], _ => "Exposed"));

        Assert.Contains("Exposed.Edit", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEditPermissionOfAnAreaIsNeverMarked()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => new PermissionCatalog([
            new PermissionDescriptor(View, IsGlobal: false),
            new PermissionDescriptor(Edit, IsGlobal: false, OnlyForAssignee: true),
        ]));

        Assert.Contains(Edit, refused.Message, StringComparison.Ordinal);
        Assert.Contains("OnlyForAssignee", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMarkedAlternativeOnAnEntityWithNoDepartmentIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => Catalogue.VerifyAlternatives([typeof(MarkedWithNoDepartment)]));

        Assert.Contains(nameof(MarkedWithNoDepartment), refused.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IOwnedByDepartment), refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMarkedPermissionOpenToTheStakeholderIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => Catalogue.VerifyAlternatives([typeof(MarkedButOpenToTheStakeholder)]));

        Assert.Contains(nameof(MarkedButOpenToTheStakeholder), refused.Message, StringComparison.Ordinal);
        Assert.Contains(Mark, refused.Message, StringComparison.Ordinal);
        Assert.Contains("DeniedToStakeholder", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatTheRulesCanHonourPasses() =>
        Assert.Null(Record.Exception(() => Catalogue.VerifyAlternatives(
            [typeof(WrittenAsTheRulesSay), typeof(UnmarkedWithNoDepartment)])));
}
