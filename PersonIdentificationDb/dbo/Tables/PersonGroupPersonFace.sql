CREATE TABLE [dbo].[PersonGroupPersonFace]
(
	[FaceId]             UNIQUEIDENTIFIER NOT NULL,
    [PersonId]           UNIQUEIDENTIFIER NOT NULL,
    [BlobName]           VARCHAR (100)    NOT NULL,
    [BlobUrl]            VARCHAR (250)    NOT NULL,
	[CreatedBy]          VARCHAR (50)     NOT NULL DEFAULT 'system',
    [CreatedDate]        DATETIME         DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]         VARCHAR (50)     NULL,
    [ModifiedDate]       DATETIME         NULL,
    PRIMARY KEY CLUSTERED ([FaceId] ASC),
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[PersonGroupPerson] ([PersonId])
);