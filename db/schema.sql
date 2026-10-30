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

                    EXEC(N'
                    UPDATE r SET r.Remarks = s.Result
                    FROM LabReports r JOIN StoolFecalysisReports s ON s.LabReportId = r.Id
                    WHERE LTRIM(RTRIM(ISNULL(s.Result, N''''))) <> N'''' AND LTRIM(RTRIM(ISNULL(r.Remarks, N''''))) = N'''';

                    UPDATE s SET s.Others = s.Result
                    FROM StoolFecalysisReports s JOIN LabReports r ON r.Id = s.LabReportId
                    WHERE LTRIM(RTRIM(ISNULL(s.Result, N''''))) <> N'''' AND r.Remarks <> s.Result AND s.Others = N''''');
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

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010094453_SignatoryLicenseNumbers'
)
BEGIN
    ALTER TABLE [StoolFecalysisReports] ADD [MedicalTechnologist2License] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010094453_SignatoryLicenseNumbers'
)
BEGIN
    ALTER TABLE [LookupValues] ADD [LicenseNo] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010094453_SignatoryLicenseNumbers'
)
BEGIN
    ALTER TABLE [LabReports] ADD [MedicalTechnologistLicense] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010094453_SignatoryLicenseNumbers'
)
BEGIN
    ALTER TABLE [LabReports] ADD [PathologistLicense] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010094453_SignatoryLicenseNumbers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010094453_SignatoryLicenseNumbers', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[UrinalysisReports].[Albumin]', N'Protein', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[UrinalysisReports].[Sugar]', N'Glucose', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[HematologyReports].[SegmentersNValue]', N'NeutrophilsNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[HematologyReports].[SegmentersResult]', N'NeutrophilsResult', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[SGPTNValue]', N'ALTSGPTNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[SGPTResult]', N'ALTSGPTResult', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistry2Reports].[SGOTCNValue]', N'ASTSGOTCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistry2Reports].[SGOTCUnit]', N'ASTSGOTCUnit', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistry2Reports].[SGOTCResults]', N'ASTSGOTCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistry2Reports].[SGOTSNValue]', N'ASTSGOTSNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistry2Reports].[SGOTSUnit]', N'ASTSGOTSUnit', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistry2Reports].[SGOTSResults]', N'ASTSGOTSResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN

                    UPDATE LookupValues SET FieldName = N'Protein' WHERE ModuleId = 6 AND FieldName = N'Albumin';
                    UPDATE LookupValues SET FieldName = N'Glucose' WHERE ModuleId = 6 AND FieldName = N'Sugar';
                    UPDATE LookupValues SET FieldName = N'Reaction (PH)' WHERE ModuleId = 6 AND FieldName = N'Reaction';
                    UPDATE ModuleDefaults SET Defaults = REPLACE(REPLACE(Defaults, N'"Albumin"', N'"Protein"'), N'"Sugar"', N'"Glucose"') WHERE ModuleId = 6;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"Segmenters', N'"Neutrophils') WHERE ModuleId = 7;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"SGPT', N'"ALTSGPT') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"SGOT', N'"ASTSGOT') WHERE ModuleId = 13;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010121232_ClientFieldRenames'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010121232_ClientFieldRenames', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125123_SharedSecondTechnologist'
)
BEGIN
    ALTER TABLE [LabReports] ADD [MedicalTechnologist2] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125123_SharedSecondTechnologist'
)
BEGIN
    ALTER TABLE [LabReports] ADD [MedicalTechnologist2License] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125123_SharedSecondTechnologist'
)
BEGIN

                    EXEC(N'UPDATE r SET r.MedicalTechnologist2 = s.MedicalTechnologist2, r.MedicalTechnologist2License = s.MedicalTechnologist2License
                    FROM LabReports r JOIN StoolFecalysisReports s ON s.LabReportId = r.Id;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125123_SharedSecondTechnologist'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StoolFecalysisReports]') AND [c].[name] = N'MedicalTechnologist2');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [StoolFecalysisReports] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [StoolFecalysisReports] DROP COLUMN [MedicalTechnologist2];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125123_SharedSecondTechnologist'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StoolFecalysisReports]') AND [c].[name] = N'MedicalTechnologist2License');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [StoolFecalysisReports] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [StoolFecalysisReports] DROP COLUMN [MedicalTechnologist2License];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125123_SharedSecondTechnologist'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010125123_SharedSecondTechnologist', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[FBSNValue]', N'FastingBloodSugarCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[FBSResult]', N'FastingBloodSugarCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[TotalCholesterolNValue]', N'CholesterolCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[TotalCholesterolResult]', N'CholesterolCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[TriglyceridesNValue]', N'TriglyceridesCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[TriglyceridesResult]', N'TriglyceridesCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[HDLNValue]', N'HDLCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[HDLResult]', N'HDLCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[LDLNValue]', N'LDLCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[LDLResult]', N'LDLCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[CreatinineNValue]', N'CreatinineCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[CreatinineResult]', N'CreatinineCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[BUNNValue]', N'BloodUreaNitrogenCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[BUNResult]', N'BloodUreaNitrogenCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[BloodUricAcidNValue]', N'BloodUricAcidCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[BloodUricAcidResult]', N'BloodUricAcidCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[ALTSGPTNValue]', N'ALTSGPTCNValue', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    EXEC sp_rename N'[ClinicalChemistryReports].[ALTSGPTResult]', N'ALTSGPTCResults', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [FastingBloodSugarCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [FastingBloodSugarSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [FastingBloodSugarSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [FastingBloodSugarSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CholesterolCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CholesterolSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CholesterolSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CholesterolSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [TriglyceridesCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [TriglyceridesSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [TriglyceridesSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [TriglyceridesSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [HDLCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [HDLSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [HDLSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [HDLSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [LDLCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [LDLSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [LDLSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [LDLSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CreatinineCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CreatinineSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CreatinineSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [CreatinineSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUreaNitrogenCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUreaNitrogenSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUreaNitrogenSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUreaNitrogenSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUricAcidCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUricAcidSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUricAcidSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [BloodUricAcidSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ALTSGPTCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ALTSGPTSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ALTSGPTSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ALTSGPTSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ASTSGOTCNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ASTSGOTCUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ASTSGOTCResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ASTSGOTSNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ASTSGOTSUnit] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    ALTER TABLE [ClinicalChemistryReports] ADD [ASTSGOTSResults] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN

                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"FBSNormalValue"', N'"FastingBloodSugarConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"TotalCholesterolNormalValue"', N'"CholesterolConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"TriglyceridesNormalValue"', N'"TriglyceridesConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"HDLNormalValue"', N'"HDLConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"LDLNormalValue"', N'"LDLConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"CreatinineNormalValue"', N'"CreatinineConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"BUNNormalValue"', N'"BloodUreaNitrogenConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"BloodUricAcidNormalValue"', N'"BloodUricAcidConventionalNormalValue"') WHERE ModuleId = 11;
                    UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'"ALTSGPTNormalValue"', N'"ALTSGPTConventionalNormalValue"') WHERE ModuleId = 11;
                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 11) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 11)
                        INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (11, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."FastingBloodSugarConventionalNormalValue"', N'60-121') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."FastingBloodSugarConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."FastingBloodSugarConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."FastingBloodSugarConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."FastingBloodSugarSystemNormalValue"', N'3.34-6.73') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."FastingBloodSugarSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."FastingBloodSugarSystemUnit"', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."FastingBloodSugarSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CholesterolConventionalNormalValue"', N'50-200') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CholesterolConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CholesterolConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CholesterolConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CholesterolSystemNormalValue"', N'1.29-5.18') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CholesterolSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CholesterolSystemUnit"', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CholesterolSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."TriglyceridesConventionalNormalValue"', N'0-150') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."TriglyceridesConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."TriglyceridesConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."TriglyceridesConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."TriglyceridesSystemNormalValue"', N'0-1.70') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."TriglyceridesSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."TriglyceridesSystemUnit"', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."TriglyceridesSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HDLConventionalNormalValue"', N'35-80') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HDLConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HDLConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HDLConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HDLSystemNormalValue"', N'0.91-2.08') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HDLSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HDLSystemUnit"', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HDLSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."LDLConventionalNormalValue"', N'66-178') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."LDLConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."LDLConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."LDLConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."LDLSystemNormalValue"', N'1.72-4.63') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."LDLSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."LDLSystemUnit"', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."LDLSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CreatinineConventionalNormalValue"', N'0.70-1.40') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CreatinineConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CreatinineConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CreatinineConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CreatinineSystemNormalValue"', N'62.0-124.0') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CreatinineSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CreatinineSystemUnit"', N'umol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CreatinineSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUreaNitrogenConventionalNormalValue"', N'7.80-20.17') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUreaNitrogenConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUreaNitrogenConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUreaNitrogenConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUreaNitrogenSystemNormalValue"', N'2.8-7.2') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUreaNitrogenSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUreaNitrogenSystemUnit"', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUreaNitrogenSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUricAcidConventionalNormalValue"', N'2.35-7.06') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUricAcidConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUricAcidConventionalUnit"', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUricAcidConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUricAcidSystemNormalValue"', N'0.14-0.42') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUricAcidSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodUricAcidSystemUnit"', N'umol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodUricAcidSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ALTSGPTConventionalNormalValue"', N'0-32') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ALTSGPTConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ALTSGPTConventionalUnit"', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ALTSGPTConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ALTSGPTSystemNormalValue"', N'0-32') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ALTSGPTSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ALTSGPTSystemUnit"', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ALTSGPTSystemUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ASTSGOTConventionalNormalValue"', N'0-31') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ASTSGOTConventionalNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ASTSGOTConventionalUnit"', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ASTSGOTConventionalUnit"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ASTSGOTSystemNormalValue"', N'0-31') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ASTSGOTSystemNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ASTSGOTSystemUnit"', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ASTSGOTSystemUnit"') IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010125558_ClinicalChemistryRework'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010125558_ClinicalChemistryRework', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010155559_HematologyRework'
)
BEGIN
    ALTER TABLE [HematologyReports] ADD [RBCCountNValue] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010155559_HematologyRework'
)
BEGIN
    ALTER TABLE [HematologyReports] ADD [RBCCountResult] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010155559_HematologyRework'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 7) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 7)
                        INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (7, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HematocritNormalValue"', N'MALE: 42.0-52.0%    FEMALE: 37.0-47.0%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HematocritNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HemoglobinNormalValue"', N'MALE: 140-170 g/L    FEMALE: 120-155 g/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HemoglobinNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."RBCCountNormalValue"', N'3.50-5.50 ' + NCHAR(215) + N'10' + NCHAR(185) + NCHAR(178) + N'/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."RBCCountNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."WBCCountNormalValue"', N'5.0-10.0 ' + NCHAR(215) + N'10' + NCHAR(8313) + N'/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."WBCCountNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."NeutrophilsNormalValue"', N'50.0-70.0%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."NeutrophilsNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."LymphocytesNormalValue"', N'10.0-40.0%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."LymphocytesNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."EosinophilsNormalValue"', N'0.0-0.05%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."EosinophilsNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."MonocytesNormalValue"', N'0.0-0.07%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."MonocytesNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BasophilsNormalValue"', N'0.0-0.01%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BasophilsNormalValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."PlateletCountNormalValue"', N'150-400 ' + NCHAR(215) + N'10' + NCHAR(8313) + N'/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."PlateletCountNormalValue"') IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010155559_HematologyRework'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010155559_HematologyRework', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010164750_PhysicalExamination'
)
BEGIN
    CREATE TABLE [PhysicalExaminationReports] (
        [LabReportId] bigint NOT NULL,
        [CbcDate] date NULL,
        [BloodTypingDate] date NULL,
        [UrinalysisDate] date NULL,
        [FecalysisDate] date NULL,
        [HematocritNValue] nvarchar(100) NULL,
        [HematocritFemaleNValue] nvarchar(100) NULL,
        [HematocritResult] nvarchar(100) NULL,
        [HemoglobinNValue] nvarchar(100) NULL,
        [HemoglobinFemaleNValue] nvarchar(100) NULL,
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
        [BloodTyping] nvarchar(50) NULL,
        [RhTyping] nvarchar(50) NULL,
        [UrineColor] nvarchar(50) NULL,
        [UrineAppearance] nvarchar(50) NULL,
        [UrineReaction] nvarchar(50) NULL,
        [UrineSPGravity] nvarchar(50) NULL,
        [UrineAlbumin] nvarchar(50) NULL,
        [UrineSugar] nvarchar(50) NULL,
        [UrinePusCells] nvarchar(50) NULL,
        [UrineRedCells] nvarchar(50) NULL,
        [UrineMucusThreads] nvarchar(50) NULL,
        [UrineEpithelialCells] nvarchar(50) NULL,
        [UrineAmorphousUratesPO4] nvarchar(50) NULL,
        [UrineBacteria] nvarchar(50) NULL,
        [UrineCrystals] nvarchar(50) NULL,
        [UrineCasts] nvarchar(50) NULL,
        [FecalysisColor] nvarchar(50) NULL,
        [FecalysisConsistency] nvarchar(50) NULL,
        [UrineOthers] nvarchar(500) NULL,
        [FecalysisResult] nvarchar(500) NULL,
        [Others] nvarchar(500) NULL,
        CONSTRAINT [PK_PhysicalExaminationReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_PhysicalExaminationReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010164750_PhysicalExamination'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 29)
                    BEGIN
                        DECLARE @serviceId bigint = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Physical Examination' ORDER BY Id);
                        IF @serviceId IS NULL
                        BEGIN
                            INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (N'Physical Examination', N'Physical Examination', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                            SET @serviceId = SCOPE_IDENTITY();
                        END
                        INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                        SELECT 29, ModuleTypeId, N'Physical Examination', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 14, 1, @serviceId FROM Modules WHERE Id = 6;
                    END
                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 29) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 29)
                        INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (29, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HematocritNValue"', N'0.42 - 0.52') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HematocritNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HematocritFemaleNValue"', N'0.37 - 0.47') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HematocritFemaleNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HemoglobinNValue"', N'140 - 170 g/l') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HemoglobinNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."HemoglobinFemaleNValue"', N'120 - 150 g/l') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."HemoglobinFemaleNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."WBCCountNValue"', N'5 - 10 x 10' + NCHAR(8313) + N'/L') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."WBCCountNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."SegmentersNValue"', N'0.50 - 0.70') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."SegmentersNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."LymphocytesNValue"', N'0.10 - 0.40') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."LymphocytesNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."EosinophilsNValue"', N'0 - 0.05') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."EosinophilsNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."MonocytesNValue"', N'0 - 0.07') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."MonocytesNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BasophilsNValue"', N'0 - 0.01') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BasophilsNValue"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."StabNValue"', N'0 - 0.05') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."StabNValue"') IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010164750_PhysicalExamination'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010164750_PhysicalExamination', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010175023_ClinicalMicroscopy'
)
BEGIN
    CREATE TABLE [ClinicalMicroscopyReports] (
        [LabReportId] bigint NOT NULL,
        [ColorResult] nvarchar(50) NULL,
        [ColorReference] nvarchar(100) NULL,
        [TransparencyResult] nvarchar(50) NULL,
        [TransparencyReference] nvarchar(100) NULL,
        [PHResult] nvarchar(50) NULL,
        [PHReference] nvarchar(100) NULL,
        [SpecificGravityResult] nvarchar(50) NULL,
        [SpecificGravityReference] nvarchar(100) NULL,
        [ProteinResult] nvarchar(50) NULL,
        [ProteinReference] nvarchar(100) NULL,
        [GlucoseResult] nvarchar(50) NULL,
        [GlucoseReference] nvarchar(100) NULL,
        [BloodResult] nvarchar(50) NULL,
        [BloodReference] nvarchar(100) NULL,
        [LeukocytesResult] nvarchar(50) NULL,
        [LeukocytesReference] nvarchar(100) NULL,
        [NitriteResult] nvarchar(50) NULL,
        [NitriteReference] nvarchar(100) NULL,
        [BilirubinResult] nvarchar(50) NULL,
        [BilirubinReference] nvarchar(100) NULL,
        [UrobilinogenResult] nvarchar(50) NULL,
        [UrobilinogenReference] nvarchar(100) NULL,
        [KetonesResult] nvarchar(50) NULL,
        [KetonesReference] nvarchar(100) NULL,
        [WBCResult] nvarchar(50) NULL,
        [WBCReference] nvarchar(100) NULL,
        [RBCResult] nvarchar(50) NULL,
        [RBCReference] nvarchar(100) NULL,
        [MucusThreadsResult] nvarchar(50) NULL,
        [MucusThreadsReference] nvarchar(100) NULL,
        [EpithelialCellsResult] nvarchar(50) NULL,
        [EpithelialCellsReference] nvarchar(100) NULL,
        [AmorphousUratesPhosphatesResult] nvarchar(50) NULL,
        [AmorphousUratesPhosphatesReference] nvarchar(100) NULL,
        [BacteriaResult] nvarchar(50) NULL,
        [BacteriaReference] nvarchar(100) NULL,
        [CrystalsResult] nvarchar(50) NULL,
        [CrystalsReference] nvarchar(100) NULL,
        [CastsResult] nvarchar(50) NULL,
        [CastsReference] nvarchar(100) NULL,
        CONSTRAINT [PK_ClinicalMicroscopyReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_ClinicalMicroscopyReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010175023_ClinicalMicroscopy'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 30)
                    BEGIN
                        DECLARE @serviceId bigint = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Clinical Microscopy' ORDER BY Id);
                        IF @serviceId IS NULL
                        BEGIN
                            INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (N'Clinical Microscopy', N'Clinical Microscopy', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                            SET @serviceId = SCOPE_IDENTITY();
                        END
                        INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                        SELECT 30, ModuleTypeId, N'Clinical Microscopy', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 15, 1, @serviceId FROM Modules WHERE Id = 6;
                    END
                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 30) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 30)
                        INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (30, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."PHReference"', N'5.0 - 8.0') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."PHReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."SpecificGravityReference"', N'1.005 - 1.030') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."SpecificGravityReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."ProteinReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."ProteinReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."GlucoseReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."GlucoseReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BloodReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BloodReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."LeukocytesReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."LeukocytesReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."NitriteReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."NitriteReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."BilirubinReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."BilirubinReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."UrobilinogenReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."UrobilinogenReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."KetonesReference"', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."KetonesReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."WBCReference"', N'0-5/hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."WBCReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."RBCReference"', N'0-2/hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."RBCReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CrystalsReference"', N'hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CrystalsReference"') IS NULL;
                    UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$."CastsReference"', N'lpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$."CastsReference"') IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261010175023_ClinicalMicroscopy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261010175023_ClinicalMicroscopy', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011052629_HideMedicalExaminationModules'
)
BEGIN
    UPDATE Modules SET IsActive = 0 WHERE Id IN (14, 15);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011052629_HideMedicalExaminationModules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011052629_HideMedicalExaminationModules', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012010550_BatchPrintModule'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 24) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 31)
                        INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                        SELECT 31, ModuleTypeId, N'Batch Print', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 3, 1, NULL FROM Modules WHERE Id = 24;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012010550_BatchPrintModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012010550_BatchPrintModule', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE TABLE [StatementsOfAccount] (
        [Id] bigint NOT NULL IDENTITY,
        [StatementNumber] nvarchar(30) NOT NULL,
        [Mode] int NOT NULL,
        [CompanyId] bigint NULL,
        [CompanyName] nvarchar(200) NULL,
        [BatchName] nvarchar(200) NULL,
        [Attention] nvarchar(200) NULL,
        [PatientId] bigint NULL,
        [PatientName] nvarchar(200) NULL,
        [PatientCode] nvarchar(200) NULL,
        [DateFrom] date NULL,
        [DateTo] date NULL,
        [GrossTotal] decimal(18,4) NOT NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [PaidTotal] decimal(18,4) NOT NULL,
        [BalanceTotal] decimal(18,4) NOT NULL,
        [IsVoid] bit NOT NULL,
        [VoidedAtUtc] datetime2(3) NULL,
        [VoidedByUserId] bigint NULL,
        [RegeneratedFromId] bigint NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StatementsOfAccount] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StatementsOfAccount_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StatementsOfAccount_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Patients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StatementsOfAccount_StatementsOfAccount_RegeneratedFromId] FOREIGN KEY ([RegeneratedFromId]) REFERENCES [StatementsOfAccount] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StatementsOfAccount_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StatementsOfAccount_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE TABLE [StatementOfAccountLines] (
        [Id] bigint NOT NULL IDENTITY,
        [StatementOfAccountId] bigint NOT NULL,
        [Sequence] int NOT NULL,
        [PatientRegistrationId] bigint NOT NULL,
        [RegistrationCode] nvarchar(100) NOT NULL,
        [PatientName] nvarchar(200) NOT NULL,
        [RegistrationDate] date NOT NULL,
        [Gross] decimal(18,4) NOT NULL,
        [Discount] decimal(18,4) NOT NULL,
        [Net] decimal(18,4) NOT NULL,
        [Paid] decimal(18,4) NOT NULL,
        [Balance] decimal(18,4) NOT NULL,
        CONSTRAINT [PK_StatementOfAccountLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StatementOfAccountLines_StatementsOfAccount_StatementOfAccountId] FOREIGN KEY ([StatementOfAccountId]) REFERENCES [StatementsOfAccount] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE INDEX [IX_StatementOfAccountLines_StatementOfAccountId_Sequence] ON [StatementOfAccountLines] ([StatementOfAccountId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE INDEX [IX_StatementsOfAccount_CompanyId] ON [StatementsOfAccount] ([CompanyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE INDEX [IX_StatementsOfAccount_CreatedAtUtc] ON [StatementsOfAccount] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE INDEX [IX_StatementsOfAccount_PatientId] ON [StatementsOfAccount] ([PatientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE INDEX [IX_StatementsOfAccount_RegeneratedFromId] ON [StatementsOfAccount] ([RegeneratedFromId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StatementsOfAccount_StatementNumber] ON [StatementsOfAccount] ([StatementNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN

                    UPDATE Modules SET HasCreate = 1, HasDelete = 1 WHERE Id = 25;
                    UPDATE UserPermissions SET AllowCreate = 1, AllowDelete = 1 WHERE ModuleId = 25 AND AllowPrint = 1;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012104153_StatementsOfAccount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012104153_StatementsOfAccount', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN
    CREATE TABLE [CustomForms] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [ModuleId] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_CustomForms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomForms_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomForms_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomForms_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN
    CREATE TABLE [CustomFormVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [CustomFormId] bigint NOT NULL,
        [VersionNumber] int NOT NULL,
        [DefinitionJson] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId] bigint NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedByUserId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CustomFormVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomFormVersions_CustomForms_CustomFormId] FOREIGN KEY ([CustomFormId]) REFERENCES [CustomForms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomFormVersions_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomFormVersions_Users_UpdatedByUserId] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN
    CREATE TABLE [CustomFormReports] (
        [LabReportId] bigint NOT NULL,
        [CustomFormVersionId] bigint NOT NULL,
        [ValuesJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_CustomFormReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_CustomFormReports_CustomFormVersions_CustomFormVersionId] FOREIGN KEY ([CustomFormVersionId]) REFERENCES [CustomFormVersions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomFormReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN
    CREATE INDEX [IX_CustomFormReports_CustomFormVersionId] ON [CustomFormReports] ([CustomFormVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomForms_ModuleId] ON [CustomForms] ([ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomFormVersions_CustomFormId_VersionNumber] ON [CustomFormVersions] ([CustomFormId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 19) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 32)
                        INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                        SELECT 32, ModuleTypeId, N'Form Designer', 1, 1, 1, 0, 0, 0, 0, 0, Icon, 8, 1, NULL FROM Modules WHERE Id = 19;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016042837_CustomForms'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016042837_CustomForms', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016065144_RenameFormDesignerModule'
)
BEGIN
    UPDATE Modules SET ModuleName = N'Custom Lab Form' WHERE Id = 32 AND ModuleName = N'Form Designer';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016065144_RenameFormDesignerModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016065144_RenameFormDesignerModule', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016102312_MedicalExamReport'
)
BEGIN
    CREATE TABLE [MedicalExamReports] (
        [LabReportId] bigint NOT NULL,
        [CivilStatus] nvarchar(50) NULL,
        [ContactNo] nvarchar(100) NULL,
        [HxEyeDisease] nvarchar(20) NULL,
        [HxEarInfection] nvarchar(20) NULL,
        [HxHearingDefect] nvarchar(20) NULL,
        [HxSinusitis] nvarchar(20) NULL,
        [HxNoseBleeding] nvarchar(20) NULL,
        [HxCoughColds] nvarchar(20) NULL,
        [HxTonsillitisPharyngitis] nvarchar(20) NULL,
        [HxNeckMassGoiter] nvarchar(20) NULL,
        [HxAsthma] nvarchar(20) NULL,
        [HxTuberculosis] nvarchar(20) NULL,
        [HxPneumonia] nvarchar(20) NULL,
        [HxDifficultyBreathing] nvarchar(20) NULL,
        [HxHypertension] nvarchar(20) NULL,
        [HxHypotension] nvarchar(20) NULL,
        [HxAnemia] nvarchar(20) NULL,
        [HxChestPainPalpitations] nvarchar(20) NULL,
        [HxHeartDisease] nvarchar(20) NULL,
        [HxBreastMass] nvarchar(20) NULL,
        [HxDiabetes] nvarchar(20) NULL,
        [HxUlcerHyperacidity] nvarchar(20) NULL,
        [HxAbdominalPain] nvarchar(20) NULL,
        [HxDiarrhea] nvarchar(20) NULL,
        [HxKidneyDiseaseUti] nvarchar(20) NULL,
        [HxHernia] nvarchar(20) NULL,
        [HxHemorrhoids] nvarchar(20) NULL,
        [HxLossOfConsciousness] nvarchar(20) NULL,
        [HxBackPainScoliosis] nvarchar(20) NULL,
        [HxHeadacheMigraine] nvarchar(20) NULL,
        [HxDizzinessVertigo] nvarchar(20) NULL,
        [HxEpilepsyConvulsion] nvarchar(20) NULL,
        [HxEasyFatigability] nvarchar(20) NULL,
        [HxArthritisJointPain] nvarchar(20) NULL,
        [HxFracture] nvarchar(20) NULL,
        [HxMeasles] nvarchar(20) NULL,
        [HxChickenPox] nvarchar(20) NULL,
        [HxMumps] nvarchar(20) NULL,
        [HxHepatitis] nvarchar(20) NULL,
        [HxMalaria] nvarchar(20) NULL,
        [HxStd] nvarchar(20) NULL,
        [HxAllergies] nvarchar(20) NULL,
        [HxSkinDisease] nvarchar(20) NULL,
        [HxPreviousAccident] nvarchar(20) NULL,
        [HxPreviousHospitalization] nvarchar(20) NULL,
        [HxPreviousOperation] nvarchar(20) NULL,
        [HxOthers] nvarchar(500) NULL,
        [HxMedication] nvarchar(500) NULL,
        [FamCancer] nvarchar(20) NULL,
        [FamHypertension] nvarchar(20) NULL,
        [FamDiabetes] nvarchar(20) NULL,
        [FamOthers] nvarchar(500) NULL,
        [MenstrualPattern] nvarchar(20) NULL,
        [Lmp] nvarchar(50) NULL,
        [Dysmenorrhea] nvarchar(20) NULL,
        [PregnantNow] nvarchar(20) NULL,
        [NumPregnancies] nvarchar(50) NULL,
        [NumMiscarriages] nvarchar(50) NULL,
        [Myoma] nvarchar(20) NULL,
        [OvarianCyst] nvarchar(20) NULL,
        [ObOthers] nvarchar(500) NULL,
        [SmokingStatus] nvarchar(20) NULL,
        [SmokingSince] nvarchar(50) NULL,
        [SticksPerDay] nvarchar(50) NULL,
        [DrinkingStatus] nvarchar(20) NULL,
        [DrinkingSince] nvarchar(50) NULL,
        [Bottles] nvarchar(50) NULL,
        [DrinkingFrequency] nvarchar(20) NULL,
        [Bp1] nvarchar(50) NULL,
        [Bp2] nvarchar(50) NULL,
        [Pulse1] nvarchar(50) NULL,
        [Pulse2] nvarchar(50) NULL,
        [Temperature] nvarchar(50) NULL,
        [Height] nvarchar(50) NULL,
        [Weight] nvarchar(50) NULL,
        [BmiCategory] nvarchar(500) NULL,
        [VisionFarWithR] nvarchar(50) NULL,
        [VisionFarWithL] nvarchar(50) NULL,
        [VisionFarWithoutR] nvarchar(50) NULL,
        [VisionFarWithoutL] nvarchar(50) NULL,
        [Ishihara] nvarchar(100) NULL,
        [RosSkin] nvarchar(20) NULL,
        [RosHeadScalp] nvarchar(20) NULL,
        [RosEyes] nvarchar(20) NULL,
        [RosEars] nvarchar(20) NULL,
        [RosNose] nvarchar(20) NULL,
        [RosTeethThroat] nvarchar(20) NULL,
        [RosNeckThyroid] nvarchar(20) NULL,
        [RosThoraxBreast] nvarchar(20) NULL,
        [RosHeartLungs] nvarchar(20) NULL,
        [RosAbdomen] nvarchar(20) NULL,
        [RosInguinalGenitalia] nvarchar(20) NULL,
        [RosExtremitiesSpine] nvarchar(20) NULL,
        [RosTattoo] nvarchar(20) NULL,
        [RosMassCyst] nvarchar(20) NULL,
        [RosOthers] nvarchar(20) NULL,
        [RosOthersNote] nvarchar(100) NULL,
        [Findings] nvarchar(1000) NULL,
        [LabChestXray] nvarchar(20) NULL,
        [LabChestXrayNote] nvarchar(200) NULL,
        [LabCbc] nvarchar(20) NULL,
        [LabCbcNote] nvarchar(200) NULL,
        [LabUrinalysis] nvarchar(20) NULL,
        [LabUrinalysisNote] nvarchar(200) NULL,
        [LabFecalysis] nvarchar(20) NULL,
        [LabFecalysisNote] nvarchar(200) NULL,
        [Classification] nvarchar(20) NULL,
        [Assessment] nvarchar(1000) NULL,
        [VitalSignsBy] nvarchar(100) NULL,
        [VitalSignsByLicense] nvarchar(50) NULL,
        [InterviewBy] nvarchar(100) NULL,
        [InterviewByLicense] nvarchar(50) NULL,
        [AssessmentBy] nvarchar(100) NULL,
        [AssessmentByLicense] nvarchar(50) NULL,
        [NotedBy] nvarchar(100) NULL,
        [NotedByLicense] nvarchar(50) NULL,
        CONSTRAINT [PK_MedicalExamReports] PRIMARY KEY ([LabReportId]),
        CONSTRAINT [FK_MedicalExamReports_LabReports_LabReportId] FOREIGN KEY ([LabReportId]) REFERENCES [LabReports] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016102312_MedicalExamReport'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 33)
                    BEGIN
                        DECLARE @serviceId bigint = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Medical Examination Report' ORDER BY Id);
                        IF @serviceId IS NULL
                        BEGIN
                            INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (N'Medical Examination Report', N'Medical Examination Report', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                            SET @serviceId = SCOPE_IDENTITY();
                        END
                        INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                        SELECT 33, ModuleTypeId, N'Medical Examination Report', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 16, 1, @serviceId FROM Modules WHERE Id = 6;
                    END
                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 33) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 33)
                        INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (33, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016102312_MedicalExamReport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016102312_MedicalExamReport', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017084114_StationVersions'
)
BEGIN
    CREATE TABLE [StationVersions] (
        [Id] int NOT NULL IDENTITY,
        [StationName] nvarchar(100) NOT NULL,
        [AppVersion] nvarchar(50) NOT NULL,
        [LastUserName] nvarchar(200) NULL,
        [FirstSeenUtc] datetime2(3) NOT NULL,
        [LastSeenUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_StationVersions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017084114_StationVersions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StationVersions_StationName] ON [StationVersions] ([StationName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017084114_StationVersions'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM Modules WHERE Id = 27) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 34)
                        INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                        SELECT 34, ModuleTypeId, N'Stations', 1, 0, 0, 0, 0, 0, 0, 0, Icon, SortOrder + 1, 1, NULL FROM Modules WHERE Id = 27;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017084114_StationVersions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017084114_StationVersions', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030071634_MergeAnnualPhysicalExamPage2'
)
BEGIN
    -- 1. Saved Page 2 results become Page 2 of an Annual Physical Exam
    DECLARE @pairs TABLE (Page2Id bigint PRIMARY KEY, ApeId bigint NOT NULL UNIQUE);
    WITH p AS (
        SELECT Id, PatientRegistrationId, ROW_NUMBER() OVER (PARTITION BY PatientRegistrationId ORDER BY Id) AS n
        FROM LabReports WHERE ReportType = N'MedicalExamination' AND IsDeleted = 0 AND PatientRegistrationId IS NOT NULL),
    a AS (
        SELECT r.Id, r.PatientRegistrationId, ROW_NUMBER() OVER (PARTITION BY r.PatientRegistrationId ORDER BY r.Id) AS n
        FROM LabReports r
        WHERE r.ReportType = N'AnnualPhysicalExam' AND r.IsDeleted = 0 AND r.PatientRegistrationId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM MedicalExaminationReports x WHERE x.LabReportId = r.Id))
    INSERT INTO @pairs (Page2Id, ApeId)
    SELECT p.Id, a.Id FROM p JOIN a ON a.PatientRegistrationId = p.PatientRegistrationId AND a.n = p.n;

    -- a Page 2 of a registration that has an Annual PE: its row moves there, with its Remarks, contact number and civil status where the Annual PE has none
    UPDATE x SET LabReportId = pr.ApeId FROM MedicalExaminationReports x JOIN @pairs pr ON pr.Page2Id = x.LabReportId;
    UPDATE a SET Remarks = COALESCE(a.Remarks, m.Remarks)
    FROM LabReports a JOIN @pairs pr ON pr.ApeId = a.Id JOIN LabReports m ON m.Id = pr.Page2Id;
    UPDATE e SET ContactNo = COALESCE(e.ContactNo, x.ContactNo), CivilStatus = COALESCE(e.CivilStatus, x.CivilStatus)
    FROM AnnualPhysicalExamReports e JOIN @pairs pr ON pr.ApeId = e.LabReportId JOIN MedicalExaminationReports x ON x.LabReportId = e.LabReportId;
    UPDATE LabReports SET IsDeleted = 1, DeletedAtUtc = SYSUTCDATETIME() WHERE Id IN (SELECT Page2Id FROM @pairs);

    -- any other Page 2 becomes an Annual PE itself, with an empty first page that takes its contact number and civil status
    INSERT INTO AnnualPhysicalExamReports (LabReportId, ContactNo, CivilStatus, LMPType, VitalSignsBy, HeightWeightBy)
    SELECT m.Id, x.ContactNo, x.CivilStatus, N'', N'', N''
    FROM LabReports m LEFT JOIN MedicalExaminationReports x ON x.LabReportId = m.Id
    WHERE m.ReportType = N'MedicalExamination' AND m.Id NOT IN (SELECT Page2Id FROM @pairs)
      AND NOT EXISTS (SELECT 1 FROM AnnualPhysicalExamReports e WHERE e.LabReportId = m.Id);
    UPDATE LabReports SET ReportType = N'AnnualPhysicalExam'
    WHERE ReportType = N'MedicalExamination' AND Id NOT IN (SELECT Page2Id FROM @pairs);

    -- 2. Page 2's entry lists belong to the Annual PE now
    UPDATE v SET ModuleId = 16 FROM LookupValues v
    WHERE v.ModuleId = 17 AND NOT EXISTS (SELECT 1 FROM LookupValues o WHERE o.ModuleId = 16 AND o.FieldName = v.FieldName);

    -- 3. One Defaults: Page 2's saved defaults join the Annual PE's (a value the Annual PE already has wins)
    IF EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 17)
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 16)
            UPDATE ModuleDefaults SET ModuleId = 16 WHERE ModuleId = 17;
        ELSE
        BEGIN
            DECLARE @merged nvarchar(max) = (SELECT Defaults FROM ModuleDefaults WHERE ModuleId = 16);
            DECLARE @keys TABLE (n int IDENTITY PRIMARY KEY, [key] nvarchar(4000), value nvarchar(max));
            INSERT INTO @keys ([key], value)
            SELECT [key], value FROM OPENJSON((SELECT Defaults FROM ModuleDefaults WHERE ModuleId = 17)) WHERE type = 1;
            DECLARE @i int = 1, @last int = (SELECT COUNT(*) FROM @keys), @key nvarchar(4000), @value nvarchar(max);
            WHILE @i <= @last
            BEGIN
                SELECT @key = [key], @value = value FROM @keys WHERE n = @i;
                IF JSON_VALUE(@merged, N'$."' + @key + N'"') IS NULL
                    SET @merged = JSON_MODIFY(@merged, N'$."' + @key + N'"', @value);
                SET @i += 1;
            END;
            UPDATE ModuleDefaults SET Defaults = @merged, UpdatedAtUtc = SYSUTCDATETIME() WHERE ModuleId = 16;
            DELETE FROM ModuleDefaults WHERE ModuleId = 17;
        END;
    END;

    -- 4. The Page 2 service is deleted and its module hidden
    DECLARE @page2Service bigint = (SELECT ServiceId FROM Modules WHERE Id = 17);
    DECLARE @apeService bigint = (SELECT ServiceId FROM Modules WHERE Id = 16);
    UPDATE Modules SET ServiceId = NULL, IsActive = 0 WHERE Id = 17;
    -- (without an Annual PE service there is nothing to move the lines to: the Page 2 service is then left as it is, unlinked)
    IF @page2Service IS NOT NULL AND @apeService IS NOT NULL
    BEGIN
        -- a registration billed for both keeps its total: the Page 2 price is added to its Annual PE line, so the lines still add up to the amount due
        UPDATE a SET Price = a.Price + x.Total, UpdatedAtUtc = SYSUTCDATETIME()
        FROM PatientRegistrationServices a
        JOIN (SELECT p.PatientRegistrationId, SUM(p.Price) AS Total,
                     (SELECT MIN(o.Id) FROM PatientRegistrationServices o
                      WHERE o.PatientRegistrationId = p.PatientRegistrationId AND o.ServiceId = @apeService AND o.IsDeleted = 0) AS ApeLineId
              FROM PatientRegistrationServices p
              WHERE p.ServiceId = @page2Service AND p.IsDeleted = 0
              GROUP BY p.PatientRegistrationId) x ON x.ApeLineId = a.Id;
        DELETE s FROM PatientRegistrationServices s
        WHERE s.ServiceId = @page2Service
          AND EXISTS (SELECT 1 FROM PatientRegistrationServices o WHERE o.PatientRegistrationId = s.PatientRegistrationId AND o.ServiceId = @apeService AND o.IsDeleted = 0);
        UPDATE PatientRegistrationServices SET ServiceId = @apeService WHERE ServiceId = @page2Service;

        -- a package lists services but is priced as a whole, so dropping a line does not change what it costs
        DELETE s FROM PackageServices s
        WHERE s.ServiceId = @page2Service
          AND EXISTS (SELECT 1 FROM PackageServices o WHERE o.PackageId = s.PackageId AND o.ServiceId = @apeService);
        UPDATE PackageServices SET ServiceId = @apeService WHERE ServiceId = @page2Service;

        DELETE FROM ServiceItemQuantities WHERE ServiceId = @page2Service;
        IF NOT EXISTS (SELECT 1 FROM Modules WHERE ServiceId = @page2Service)
            DELETE FROM Services WHERE Id = @page2Service;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030071634_MergeAnnualPhysicalExamPage2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030071634_MergeAnnualPhysicalExamPage2', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030080850_RenameMedicalExamReportToWalkIn'
)
BEGIN
    UPDATE s SET ServiceName = N'Physical Examination (Walk-In)', ServiceDescription = N'Physical Examination (Walk-In)', UpdatedAtUtc = SYSUTCDATETIME()
    FROM Services s JOIN Modules m ON m.ServiceId = s.Id
    WHERE m.Id = 33 AND s.ServiceName = N'Medical Examination Report';
    UPDATE Modules SET ModuleName = N'Physical Examination (Walk-In)' WHERE Id = 33 AND ModuleName = N'Medical Examination Report';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030080850_RenameMedicalExamReportToWalkIn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030080850_RenameMedicalExamReportToWalkIn', N'10.0.12');
END;

COMMIT;
GO

