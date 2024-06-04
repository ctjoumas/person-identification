CREATE TABLE [dbo].[PersonGroupImages] (
    [PersonGroupImageId] UNIQUEIDENTIFIER NOT NULL,
    [PersonGroupId]      UNIQUEIDENTIFIER NOT NULL,
    [PersonId]           UNIQUEIDENTIFIER NOT NULL,
    [BlobName]           VARCHAR (100)    NOT NULL,
    [BlobUrl]            VARCHAR (250)    NOT NULL,
    [CreatedBy]          VARCHAR (50)     NOT NULL,
    [CreatedDate]        DATETIME         DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]         VARCHAR (50)     NULL,
    [ModifiedDate]       DATETIME         NULL,
    PRIMARY KEY CLUSTERED ([PersonGroupImageId] ASC),
    FOREIGN KEY ([PersonGroupId]) REFERENCES [dbo].[PersonGroup] ([PersonGroupId])
);

