namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Машинные имена отказов при приглашении сотрудника. См.
/// <see cref="Install.InstallErrorCodeNames"/> — та же причина: отказ нужно назвать на языке того,
/// кто его читает.
/// </summary>
public static class StaffInviteErrorCodeNames
{
    /// <summary>Номер не похож на телефон — приглашение уходит SMS, слать его некуда.</summary>
    public const string InvalidPhone = "invalid_phone";

    /// <summary>Логин уже занят другим сотрудником клуба.</summary>
    public const string UserNameTaken = "staff_username_taken";

    /// <summary>Номер уже принадлежит сотруднику клуба.</summary>
    public const string PhoneTaken = "staff_phone_taken";
}
