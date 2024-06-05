﻿CREATE TABLE [dbo].[PersonGroup] (
    [PersonGroupId] UNIQUEIDENTIFIER NOT NULL,
    [IsTrained]     BIT              NULL,
    [IsDeleted]     BIT              NULL,
    [CreatedBy]     VARCHAR (50)     NOT NULL,
    [CreatedDate]   DATETIME         DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]    VARCHAR (50)     NULL,
    [ModifiedDate]  DATETIME         NULL,
    PRIMARY KEY CLUSTERED ([PersonGroupId] ASC)
);