CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `my_MyTable` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `RowID` char(36) COLLATE ascii_general_ci NOT NULL,
    `LastChanged` datetime(6) NOT NULL,
    `LastChangedBy` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_my_MyTable` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `my_MyTableRef` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `ParentId` bigint NOT NULL,
    `MyInfo` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
    `LastChanged` datetime(6) NOT NULL,
    `LastChangedBy` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_my_MyTableRef` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_my_MyTableRef_my_MyTable_ParentId` FOREIGN KEY (`ParentId`) REFERENCES `my_MyTable` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `my_MyTable` (`Id`, `LastChanged`, `LastChangedBy`, `RowID`)
VALUES (-2, TIMESTAMP '1970-01-01 00:00:00', 'Stefan', 'a6327380-3f34-4c01-b82a-0e3733c8aab7'),
(-1, TIMESTAMP '1970-01-01 00:00:00', 'Baldr', '13fb6282-2ed6-4274-8761-2244b7b3923b');

INSERT INTO `my_MyTableRef` (`Id`, `LastChanged`, `LastChangedBy`, `MyInfo`, `ParentId`)
VALUES (-3, TIMESTAMP '1970-01-01 00:00:00', 'Stefan', 'OtherData', 2),
(-2, TIMESTAMP '1970-01-01 00:00:00', 'Baldr', 'BiggerData', 1),
(-1, TIMESTAMP '1970-01-01 00:00:00', 'Baldr', 'BigData', 1);

CREATE INDEX `IX_my_MyTable_LastChanged` ON `my_MyTable` (`LastChanged`);

CREATE UNIQUE INDEX `IX_my_MyTable_RowID` ON `my_MyTable` (`RowID`);

CREATE INDEX `IX_my_MyTableRef_MyInfo` ON `my_MyTableRef` (`MyInfo`);

CREATE INDEX `IX_my_MyTableRef_ParentId` ON `my_MyTableRef` (`ParentId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20251124153100_InitDbTest', '8.0.21');

COMMIT;

