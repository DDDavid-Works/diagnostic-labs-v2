IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [CodeSequences] (
        [Prefix] nvarchar(50) NOT NULL,
        [LastNumber] bigint NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CodeSequences] PRIMARY KEY ([Prefix])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ModuleTypes] (
        [Id] int NOT NULL,
        [ModuleTypeName] nvarchar(50) NOT NULL,
        [Icon] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsAdmin] bit NOT NULL,
        CONSTRAINT [PK_ModuleTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] bigint NOT NULL IDENTITY,
        [Username] nvarchar(100) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [PasswordHash] nvarchar(200) NOT NULL,
        [IsAdmin] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [LastLoginAtUtc] datetime2(3) NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEndUtc] datetime2(3) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Users_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(64) NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [ChangedByUserId] bigint NULL,
        [ChangedAtUtc] datetime2(3) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyName] nvarchar(200) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [ContactNumbers] nvarchar(200) NOT NULL,
        [ContactPerson] nvarchar(200) NOT NULL,
        [IsSystem] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Companies_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Companies_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [CompanySetups] (
        [Id] bigint NOT NULL IDENTITY,
        [CompanyName] nvarchar(200) NULL,
        [SubCompanyName] nvarchar(200) NULL,
        [Tagline] nvarchar(500) NULL,
        [Address] nvarchar(200) NULL,
        [ContactNumbers] nvarchar(200) NULL,
        [Email] nvarchar(200) NULL,
        [Code] nvarchar(20) NULL,
        [Logo] varbinary(max) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CompanySetups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompanySetups_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CompanySetups_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] bigint NOT NULL IDENTITY,
        [DepartmentName] nvarchar(50) NOT NULL,
        [DepartmentDescription] nvarchar(200) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Departments_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Discounts] (
        [Id] bigint NOT NULL IDENTITY,
        [DiscountName] nvarchar(50) NOT NULL,
        [DiscountDescription] nvarchar(200) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Discounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Discounts_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Discounts_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ItemLocations] (
        [Id] bigint NOT NULL IDENTITY,
        [ItemLocationName] nvarchar(50) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ItemLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ItemLocations_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ItemLocations_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Items] (
        [Id] bigint NOT NULL IDENTITY,
        [ItemName] nvarchar(50) NOT NULL,
        [Cost] decimal(18,4) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Items] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Items_Cost] CHECK ([Cost] >= 0),
        CONSTRAINT [FK_Items_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Items_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Patients] (
        [Id] bigint NOT NULL IDENTITY,
        [PatientCode] nvarchar(200) NOT NULL,
        [PatientName] nvarchar(200) NOT NULL,
        [DateOfBirth] date NULL,
        [Sex] nvarchar(20) NULL,
        [CivilStatus] nvarchar(20) NULL,
        [Address] nvarchar(500) NOT NULL,
        [ContactNumbers] nvarchar(50) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_Patients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Patients_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Patients_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Patients_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Services] (
        [Id] bigint NOT NULL IDENTITY,
        [ServiceName] nvarchar(50) NOT NULL,
        [ServiceDescription] nvarchar(200) NOT NULL,
        [Price] decimal(18,4) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Services] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Services_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_Services_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Services_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Packages] (
        [Id] bigint NOT NULL IDENTITY,
        [PackageName] nvarchar(50) NOT NULL,
        [PackageDescription] nvarchar(200) NOT NULL,
        [Price] decimal(18,4) NOT NULL,
        [CompanyId] bigint NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Packages] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Packages_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_Packages_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Packages_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Packages_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [DiscountDetails] (
        [Id] bigint NOT NULL IDENTITY,
        [DiscountId] bigint NOT NULL,
        [Amount] decimal(18,4) NULL,
        [Percentage] decimal(18,4) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_DiscountDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DiscountDetails_AmountOrPercentage] CHECK (([Amount] IS NULL AND [Percentage] IS NOT NULL) OR ([Amount] IS NOT NULL AND [Percentage] IS NULL)),
        CONSTRAINT [CK_DiscountDetails_Values] CHECK (([Amount] IS NULL OR [Amount] >= 0) AND ([Percentage] IS NULL OR [Percentage] BETWEEN 0 AND 100)),
        CONSTRAINT [FK_DiscountDetails_Discounts_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DiscountDetails_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DiscountDetails_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DiscountDetails_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ItemQuantities] (
        [Id] bigint NOT NULL IDENTITY,
        [ItemId] bigint NOT NULL,
        [ItemLocationId] bigint NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_ItemQuantities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ItemQuantities_Quantity] CHECK ([Quantity] >= 0),
        CONSTRAINT [FK_ItemQuantities_ItemLocations_ItemLocationId] FOREIGN KEY ([ItemLocationId]) REFERENCES [ItemLocations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ItemQuantities_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ItemQuantities_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ItemQuantities_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ItemQuantities_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Modules] (
        [Id] int NOT NULL,
        [ModuleTypeId] int NOT NULL,
        [ModuleName] nvarchar(50) NOT NULL,
        [HasView] bit NOT NULL,
        [HasCreate] bit NOT NULL,
        [HasEdit] bit NOT NULL,
        [HasDelete] bit NOT NULL,
        [HasSearch] bit NOT NULL,
        [HasPrint] bit NOT NULL,
        [HasShowList] bit NOT NULL,
        [HasSetDefaults] bit NOT NULL,
        [Icon] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [ServiceId] bigint NULL,
        CONSTRAINT [PK_Modules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Modules_ModuleTypes_ModuleTypeId] FOREIGN KEY ([ModuleTypeId]) REFERENCES [ModuleTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Modules_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ServiceItemQuantities] (
        [Id] bigint NOT NULL IDENTITY,
        [ServiceId] bigint NOT NULL,
        [ItemId] bigint NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_ServiceItemQuantities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ServiceItemQuantities_Quantity] CHECK ([Quantity] >= 0),
        CONSTRAINT [FK_ServiceItemQuantities_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ServiceItemQuantities_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ServiceItemQuantities_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ServiceItemQuantities_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ServiceItemQuantities_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [PackageServices] (
        [Id] bigint NOT NULL IDENTITY,
        [PackageId] bigint NOT NULL,
        [ServiceId] bigint NOT NULL,
        [Price] decimal(18,4) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_PackageServices] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PackageServices_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_PackageServices_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [Packages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PackageServices_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PackageServices_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PackageServices_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PackageServices_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [PatientRegistrations] (
        [Id] bigint NOT NULL IDENTITY,
        [RegistrationCode] nvarchar(100) NOT NULL,
        [InputDate] datetime2(3) NOT NULL,
        [PatientId] bigint NOT NULL,
        [CompanyId] bigint NULL,
        [PackageId] bigint NULL,
        [BatchName] nvarchar(200) NOT NULL,
        [AmountDue] decimal(18,4) NOT NULL,
        [DiscountAmount] decimal(18,4) NULL,
        [DiscountPercentage] decimal(18,4) NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_PatientRegistrations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PatientRegistrations_Amounts] CHECK ([AmountDue] >= 0 AND [DiscountTotal] >= 0),
        CONSTRAINT [CK_PatientRegistrations_DiscountPercentage] CHECK ([DiscountPercentage] IS NULL OR [DiscountPercentage] BETWEEN 0 AND 100),
        CONSTRAINT [FK_PatientRegistrations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrations_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [Packages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrations_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrations_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrations_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrations_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [LookupValues] (
        [Id] bigint NOT NULL IDENTITY,
        [ModuleId] int NULL,
        [Kind] int NOT NULL,
        [FieldName] nvarchar(50) NOT NULL,
        [Title] nvarchar(100) NULL,
        [Value] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_LookupValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LookupValues_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LookupValues_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LookupValues_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ModuleDefaults] (
        [Id] bigint NOT NULL IDENTITY,
        [ModuleId] int NULL,
        [Defaults] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ModuleDefaults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ModuleDefaults_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ModuleDefaults_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ModuleDefaults_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [UserPermissions] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [ModuleId] int NOT NULL,
        [AllowCreate] bit NOT NULL,
        [AllowEdit] bit NOT NULL,
        [AllowDelete] bit NOT NULL,
        [AllowPrint] bit NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissions_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserPermissions_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserPermissions_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [LabReports] (
        [Id] bigint NOT NULL IDENTITY,
        [ReportType] nvarchar(40) NOT NULL,
        [PatientId] bigint NOT NULL,
        [PatientRegistrationId] bigint NULL,
        [PatientCode] nvarchar(200) NOT NULL,
        [PatientName] nvarchar(200) NOT NULL,
        [Age] nvarchar(50) NULL,
        [Sex] nvarchar(20) NULL,
        [CompanyOrPhysician] nvarchar(200) NULL,
        [DateRequested] datetime2(3) NOT NULL,
        [Remarks] nvarchar(500) NULL,
        [MedicalTechnologist] nvarchar(100) NULL,
        [Pathologist] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_LabReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LabReports_PatientRegistrations_PatientRegistrationId] FOREIGN KEY ([PatientRegistrationId]) REFERENCES [PatientRegistrations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LabReports_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [PatientRegistrationServices] (
        [Id] bigint NOT NULL IDENTITY,
        [PatientRegistrationId] bigint NOT NULL,
        [ServiceId] bigint NOT NULL,
        [Price] decimal(18,4) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_PatientRegistrationServices] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PatientRegistrationServices_Price] CHECK ([Price] >= 0),
        CONSTRAINT [FK_PatientRegistrationServices_PatientRegistrations_PatientRegistrationId] FOREIGN KEY ([PatientRegistrationId]) REFERENCES [PatientRegistrations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrationServices_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrationServices_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrationServices_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrationServices_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] bigint NOT NULL IDENTITY,
        [PaymentDate] datetime2(3) NOT NULL,
        [PatientRegistrationId] bigint NOT NULL,
        [PaymentAmount] decimal(18,4) NOT NULL,
        [Type] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([PaymentAmount] > 0 OR ([Type] = 1 AND [PaymentAmount] >= 0)),
        CONSTRAINT [FK_Payments_PatientRegistrations_PatientRegistrationId] FOREIGN KEY ([PatientRegistrationId]) REFERENCES [PatientRegistrations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [AnnualPhysicalExamReports] (
        [LabReportId] bigint NOT NULL,
        [DepartmentOrAgency] nvarchar(100) NULL,
        [BirthDate] datetime2(3) NULL,
        [CivilStatus] nvarchar(50) NULL,
        [ContactNo] nvarchar(100) NULL,
        [ENT] nvarchar(200) NULL,
        [Gastroenterology] nvarchar(200) NULL,
        [Respiratory] nvarchar(200) NULL,
        [IntegumentarySkin] nvarchar(200) NULL,
        [Cardiology] nvarchar(200) NULL,
        [Psychology] nvarchar(200) NULL,
        [Endocrinology] nvarchar(200) NULL,
        [OBGyneUrology] nvarchar(200) NULL,
        [Muscoloskeletal] nvarchar(200) NULL,
        [InfectiousCommunicable] nvarchar(200) NULL,
        [Neurological] nvarchar(200) NULL,
        [Surgical] nvarchar(200) NULL,
        [OthersPast] nvarchar(200) NULL,
        [Medications] nvarchar(200) NULL,
        [ReviewOfSystems] nvarchar(200) NULL,
        [Allergies] nvarchar(200) NULL,
        [IsSmoking] bit NULL,
        [SmokingSinceWhen] nvarchar(200) NULL,
        [NumberOfSticksPerDay] int NULL,
        [IsDrinking] bit NULL,
        [DrinkingSinceWhen] nvarchar(200) NULL,
        [NumberOfBottles] int NULL,
        [DrinkingFrequency] nvarchar(20) NULL,
        [LMP] nvarchar(50) NULL,
        [LMPType] nvarchar(50) NOT NULL,
        [BP1st] nvarchar(20) NULL,
        [BP2nd] nvarchar(20) NULL,
        [CardiacRate1st] nvarchar(20) NULL,
        [CardiacRate2nd] nvarchar(20) NULL,
        [Height] nvarchar(20) NULL,
        [Weight] nvarchar(20) NULL,
        [BMICategory] nvarchar(50) NULL,
        [VARightEyeWGlasses] nvarchar(50) NULL,
        [VARightEyeWOGlasses] nvarchar(50) NULL,
        [VALeftEyeWGlasses] nvarchar(50) NULL,
        [VALeftEyeWOGlasses] nvarchar(50) NULL,
        [VisualAcuity] nvarchar(20) NULL,
        [Skin] nvarchar(2) NULL,
        [HeadScalp] nvarchar(2) NULL,
        [Eyes] nvarchar(2) NULL,
        [Ears] nvarchar(2) NULL,
        [Nose] nvarchar(2) NULL,
        [TeethTonsilsThroatPharynx] nvarchar(2) NULL,
        [NeckLymphNodesThyroid] nvarchar(2) NULL,
        [ThoraxBreast] nvarchar(2) NULL,
        [HeartLungs] nvarchar(2) NULL,
        [AbdomenLiverSpleen] nvarchar(2) NULL,
        [InguinalAreaGenitalsAnus] nvarchar(2) NULL,
        [ExtremetiesSpine] nvarchar(2) NULL,
        [Tattoo] nvarchar(2) NULL,
        [MassCyst] nvarchar(2) NULL,
        [OthersPE] nvarchar(2) NULL,
        [Findings] nvarchar(500) NULL,
        [VitalSignsBy] nvarchar(50) NOT NULL,
        [HeightWeightBy] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_AnnualPhysicalExamReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_AnnualPhysicalExamReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ClinicalChemistry1Reports] (
        [LabReportId] bigint NOT NULL,
        [Test] nvarchar(100) NULL,
        [Result] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_ClinicalChemistry1Reports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_ClinicalChemistry1Reports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ClinicalChemistry2Reports] (
        [LabReportId] bigint NOT NULL,
        [AlkalinePhosphataseCNValue] nvarchar(100) NULL,
        [AlkalinePhosphataseCUnit] nvarchar(100) NULL,
        [AlkalinePhosphataseCResults] nvarchar(100) NULL,
        [AlkalinePhosphataseSNValue] nvarchar(100) NULL,
        [AlkalinePhosphataseSUnit] nvarchar(100) NULL,
        [AlkalinePhosphataseSResults] nvarchar(100) NULL,
        [SGOTCNValue] nvarchar(100) NULL,
        [SGOTCUnit] nvarchar(100) NULL,
        [SGOTCResults] nvarchar(100) NULL,
        [SGOTSNValue] nvarchar(100) NULL,
        [SGOTSUnit] nvarchar(100) NULL,
        [SGOTSResults] nvarchar(100) NULL,
        CONSTRAINT [PK_ClinicalChemistry2Reports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_ClinicalChemistry2Reports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ClinicalChemistryReports] (
        [LabReportId] bigint NOT NULL,
        [FBSNValue] nvarchar(100) NULL,
        [FBSResult] nvarchar(100) NULL,
        [TotalCholesterolNValue] nvarchar(100) NULL,
        [TotalCholesterolResult] nvarchar(100) NULL,
        [TriglyceridesNValue] nvarchar(100) NULL,
        [TriglyceridesResult] nvarchar(100) NULL,
        [HDLNValue] nvarchar(100) NULL,
        [HDLResult] nvarchar(100) NULL,
        [BUNNValue] nvarchar(100) NULL,
        [BUNResult] nvarchar(100) NULL,
        [CreatinineNValue] nvarchar(100) NULL,
        [CreatinineResult] nvarchar(100) NULL,
        [BloodUricAcidNValue] nvarchar(100) NULL,
        [BloodUricAcidResult] nvarchar(100) NULL,
        [LDLNValue] nvarchar(100) NULL,
        [LDLResult] nvarchar(100) NULL,
        [SGPTNValue] nvarchar(100) NULL,
        [SGPTResult] nvarchar(100) NULL,
        CONSTRAINT [PK_ClinicalChemistryReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_ClinicalChemistryReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [HematologyReports] (
        [LabReportId] bigint NOT NULL,
        [HematocritNValue] nvarchar(100) NULL,
        [HematocritResult] nvarchar(100) NULL,
        [HemoglobinNValue] nvarchar(100) NULL,
        [HemoglobinResult] nvarchar(100) NULL,
        [WBCCountNValue] nvarchar(100) NULL,
        [WBCCountResult] nvarchar(100) NULL,
        [SegmentersNValue] nvarchar(100) NULL,
        [SegmentersResult] nvarchar(100) NULL,
        [LymphocytesNValue] nvarchar(100) NULL,
        [LymphocytesResult] nvarchar(100) NULL,
        [EosinophilsNValue] nvarchar(100) NULL,
        [EosinophilsResult] nvarchar(100) NULL,
        [MonocytesNValue] nvarchar(100) NULL,
        [MonocytesResult] nvarchar(100) NULL,
        [BasophilsNValue] nvarchar(100) NULL,
        [BasophilsResult] nvarchar(100) NULL,
        [StabNValue] nvarchar(100) NULL,
        [StabResult] nvarchar(100) NULL,
        [PlateletCountNValue] nvarchar(100) NULL,
        [PlateletCountResult] nvarchar(100) NULL,
        CONSTRAINT [PK_HematologyReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_HematologyReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [ImmunologyReports] (
        [LabReportId] bigint NOT NULL,
        [Test] nvarchar(100) NULL,
        [Result] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_ImmunologyReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_ImmunologyReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [LabReportPhotos] (
        [LabReportId] bigint NOT NULL,
        [Content] varbinary(max) NOT NULL,
        [ContentType] nvarchar(100) NULL,
        CONSTRAINT [PK_LabReportPhotos] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_LabReportPhotos_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [MedicalExaminationReports] (
        [LabReportId] bigint NOT NULL,
        [ContactNo] nvarchar(100) NULL,
        [CivilStatus] nvarchar(50) NULL,
        [ChestXray] nvarchar(10) NULL,
        [ChestXrayRemarks] nvarchar(100) NULL,
        [CBC] nvarchar(10) NULL,
        [CBCRemarks] nvarchar(100) NULL,
        [Urinalysis] nvarchar(10) NULL,
        [UrinalysisRemarks] nvarchar(100) NULL,
        [Fecalysis] nvarchar(10) NULL,
        [FecalysisRemarks] nvarchar(100) NULL,
        [HBsAg] nvarchar(10) NULL,
        [HBsAgRemarks] nvarchar(100) NULL,
        [DrugTest2Panel] nvarchar(10) NULL,
        [DrugTest2PanelRemarks] nvarchar(100) NULL,
        [DrugTest4Panel] nvarchar(10) NULL,
        [DrugTest4PanelRemarks] nvarchar(100) NULL,
        [Classification] nvarchar(10) NULL,
        [MedicalSurgicalHistory] nvarchar(500) NULL,
        [Assessment] nvarchar(500) NULL,
        [AssessmentDoneBy] nvarchar(250) NULL,
        [PhysicianName] nvarchar(250) NULL,
        [PhysicianLicense] nvarchar(250) NULL,
        CONSTRAINT [PK_MedicalExaminationReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_MedicalExaminationReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [PregnancyTestReports] (
        [LabReportId] bigint NOT NULL,
        [Result] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_PregnancyTestReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_PregnancyTestReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [SerologyReports] (
        [LabReportId] bigint NOT NULL,
        [Test] nvarchar(100) NULL,
        [Result] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_SerologyReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_SerologyReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [StoolFecalysisReports] (
        [LabReportId] bigint NOT NULL,
        [Color] nvarchar(50) NULL,
        [Consistency] nvarchar(50) NULL,
        [Result] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_StoolFecalysisReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_StoolFecalysisReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE TABLE [UrinalysisReports] (
        [LabReportId] bigint NOT NULL,
        [Color] nvarchar(50) NULL,
        [Appearance] nvarchar(50) NULL,
        [Reaction] nvarchar(50) NULL,
        [SPGravity] nvarchar(50) NULL,
        [Albumin] nvarchar(50) NULL,
        [Sugar] nvarchar(50) NULL,
        [PusCells] nvarchar(50) NULL,
        [RedCells] nvarchar(50) NULL,
        [MucusThreads] nvarchar(50) NULL,
        [EpithelialCells] nvarchar(50) NULL,
        [AmorphousUratesPO4] nvarchar(50) NULL,
        [Bacteria] nvarchar(50) NULL,
        [Casts] nvarchar(50) NULL,
        [Crystals] nvarchar(50) NULL,
        [Others] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_UrinalysisReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_UrinalysisReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ChangedAtUtc] ON [AuditLogs] ([ChangedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ChangedByUserId] ON [AuditLogs] ([ChangedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName_EntityId] ON [AuditLogs] ([EntityName], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Companies_CompanyName] ON [Companies] ([CompanyName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DiscountDetails_DiscountId] ON [DiscountDetails] ([DiscountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ItemQuantities_ItemId_ItemLocationId] ON [ItemQuantities] ([ItemId], [ItemLocationId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ItemQuantities_ItemLocationId] ON [ItemQuantities] ([ItemLocationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabReports_PatientId] ON [LabReports] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabReports_PatientRegistrationId] ON [LabReports] ([PatientRegistrationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LabReports_ReportType_DateRequested] ON [LabReports] ([ReportType], [DateRequested]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LookupValues_ModuleId_Kind_FieldName] ON [LookupValues] ([ModuleId], [Kind], [FieldName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ModuleDefaults_ModuleId] ON [ModuleDefaults] ([ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Modules_ModuleTypeId] ON [Modules] ([ModuleTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Modules_ServiceId] ON [Modules] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Packages_CompanyId] ON [Packages] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PackageServices_PackageId_ServiceId] ON [PackageServices] ([PackageId], [ServiceId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PackageServices_ServiceId] ON [PackageServices] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrations_CompanyId] ON [PatientRegistrations] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrations_InputDate] ON [PatientRegistrations] ([InputDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrations_PackageId] ON [PatientRegistrations] ([PackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrations_PatientId] ON [PatientRegistrations] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PatientRegistrations_RegistrationCode] ON [PatientRegistrations] ([RegistrationCode]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrationServices_PatientRegistrationId] ON [PatientRegistrationServices] ([PatientRegistrationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrationServices_ServiceId] ON [PatientRegistrationServices] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Patients_PatientCode] ON [Patients] ([PatientCode]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Patients_PatientName] ON [Patients] ([PatientName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PatientRegistrationId] ON [Payments] ([PatientRegistrationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PaymentDate] ON [Payments] ([PaymentDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ServiceItemQuantities_ItemId] ON [ServiceItemQuantities] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ServiceItemQuantities_ServiceId_ItemId] ON [ServiceItemQuantities] ([ServiceId], [ItemId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Services_ServiceName] ON [Services] ([ServiceName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserPermissions_ModuleId] ON [UserPermissions] ([ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPermissions_UserId_ModuleId] ON [UserPermissions] ([UserId], [ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009085414_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009085414_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009132225_RegistrationGroundwork'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Companies]') AND [c].[name] = N'IsSystem');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Companies] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Companies] DROP COLUMN [IsSystem];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009132225_RegistrationGroundwork'
)
BEGIN
    ALTER TABLE [Patients] ADD [Age] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009132225_RegistrationGroundwork'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009132225_RegistrationGroundwork', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009143610_RegistrationDiscount'
)
BEGIN
    ALTER TABLE [PatientRegistrations] ADD [DiscountId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009143610_RegistrationDiscount'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrations_DiscountId] ON [PatientRegistrations] ([DiscountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009143610_RegistrationDiscount'
)
BEGIN
    ALTER TABLE [PatientRegistrations] ADD CONSTRAINT [FK_PatientRegistrations_Discounts_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discounts] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009143610_RegistrationDiscount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009143610_RegistrationDiscount', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009180119_LabReportsOptionalPatient'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LabReports]') AND [c].[name] = N'PatientId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [LabReports] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [LabReports] ALTER COLUMN [PatientId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009180119_LabReportsOptionalPatient'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009180119_LabReportsOptionalPatient', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009194643_ModuleDefaultsUnique'
)
BEGIN
    DROP INDEX [IX_ModuleDefaults_ModuleId] ON [ModuleDefaults];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009194643_ModuleDefaultsUnique'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ModuleDefaults_ModuleId] ON [ModuleDefaults] ([ModuleId]) WHERE [ModuleId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009194643_ModuleDefaultsUnique'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009194643_ModuleDefaultsUnique', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010053554_RegistrationDiscountSteps'
)
BEGIN
    CREATE TABLE [PatientRegistrationDiscountSteps] (
        [Id] bigint NOT NULL IDENTITY,
        [PatientRegistrationId] bigint NOT NULL,
        [Sequence] int NOT NULL,
        [Amount] decimal(18,4) NULL,
        [Percentage] decimal(18,4) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2(3) NULL,
        [DeletedByUserId] bigint NULL,
        CONSTRAINT [PK_PatientRegistrationDiscountSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PatientRegistrationDiscountSteps_AmountOrPercentage] CHECK (([Amount] IS NULL AND [Percentage] IS NOT NULL) OR ([Amount] IS NOT NULL AND [Percentage] IS NULL)),
        CONSTRAINT [CK_PatientRegistrationDiscountSteps_Values] CHECK (([Amount] IS NULL OR [Amount] >= 0) AND ([Percentage] IS NULL OR [Percentage] BETWEEN 0 AND 100)),
        CONSTRAINT [FK_PatientRegistrationDiscountSteps_PatientRegistrations_PatientRegistrationId] FOREIGN KEY ([PatientRegistrationId]) REFERENCES [PatientRegistrations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrationDiscountSteps_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrationDiscountSteps_Users_DeletedByUserId] FOREIGN KEY ([DeletedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PatientRegistrationDiscountSteps_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010053554_RegistrationDiscountSteps'
)
BEGIN
    CREATE INDEX [IX_PatientRegistrationDiscountSteps_PatientRegistrationId_Sequence] ON [PatientRegistrationDiscountSteps] ([PatientRegistrationId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010053554_RegistrationDiscountSteps'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010053554_RegistrationDiscountSteps', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [Bacteria] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [FatGlobules] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [MedicalTechnologist2] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [Others] nvarchar(500) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [OvaParasite] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [Rbc] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [Wbc] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [YeastCells] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN

                    UPDATE r SET r.Remarks = s.Result
                    FROM LabReports r JOIN StoolFecalysisReports s ON s.LabReportId = r.Id
                    WHERE LTRIM(RTRIM(ISNULL(s.Result, N''))) <> N'' AND LTRIM(RTRIM(ISNULL(r.Remarks, N''))) = N'';

                    UPDATE s SET s.Others = s.Result
                    FROM StoolFecalysisReports s JOIN LabReports r ON r.Id = s.LabReportId
                    WHERE LTRIM(RTRIM(ISNULL(s.Result, N''))) <> N'' AND r.Remarks <> s.Result AND s.Others = N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010084446_FecalysisRework'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010084446_FecalysisRework', N'10.0.12');
END;

COMMIT;
GO

