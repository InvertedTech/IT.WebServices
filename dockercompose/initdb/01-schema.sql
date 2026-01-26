CREATE DATABASE IF NOT EXISTS tmpdata CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_ci;
USE tmpdata;

/*M!999999\- enable the sandbox mode */ 
-- MariaDB dump 10.19-11.5.2-MariaDB, for debian-linux-gnu (x86_64)
--
-- Host: localhost    Database: tmpdata
-- ------------------------------------------------------
-- Server version11.5.2-MariaDB-ubu2404

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*M!100616 SET @OLD_NOTE_VERBOSITY=@@NOTE_VERBOSITY, NOTE_VERBOSITY=0 */;

--
-- Table structure for table `Auth_Totp`
--

DROP TABLE IF EXISTS `Auth_Totp`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Auth_Totp` (
  `TotpID` varchar(40) NOT NULL,
  `UserID` varchar(40) NOT NULL,
  `DeviceName` varchar(100) DEFAULT NULL,
  `Key` binary(10) DEFAULT NULL,
  `CreatedOnUTC` datetime DEFAULT NULL,
  `VerifiedOnUTC` datetime DEFAULT NULL,
  `DisabledOnUTC` datetime DEFAULT NULL,
  PRIMARY KEY (`TotpID`),
  UNIQUE KEY `TotpID_UNIQUE` (`TotpID`),
  KEY `UserID_IDX` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Auth_User`
--

DROP TABLE IF EXISTS `Auth_User`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Auth_User` (
  `UserID` varchar(40) NOT NULL,
  `UserName` varchar(45) NOT NULL,
  `DisplayName` varchar(45) NOT NULL,
  `Bio` varchar(45) DEFAULT NULL,
  `Roles` varchar(1000) DEFAULT NULL,
  `Email` varchar(255) DEFAULT NULL,
  `OldUserID` varchar(100) DEFAULT NULL,
  `PasswordHash` binary(32) NOT NULL,
  `PasswordSalt` binary(16) NOT NULL,
  `OldPassword` varchar(100) DEFAULT NULL,
  `OldPasswordAlgorithm` varchar(20) DEFAULT NULL,
  `CreatedOnUTC` datetime NOT NULL,
  `CreatedBy` varchar(40) NOT NULL,
  `ModifiedOnUTC` datetime NOT NULL,
  `ModifiedBy` varchar(40) NOT NULL,
  `DisabledOnUTC` datetime DEFAULT NULL,
  `DisabledBy` varchar(40) DEFAULT NULL,
  PRIMARY KEY (`UserID`),
  UNIQUE KEY `id_UNIQUE` (`UserID`),
  UNIQUE KEY `UserName_UNIQUE` (`UserName`),
  UNIQUE KEY `Email_UNIQUE` (`Email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `CMS_Content`
--

DROP TABLE IF EXISTS `CMS_Content`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `CMS_Content` (
  `ContentID` varchar(40) NOT NULL,
  `Type` varchar(20) DEFAULT NULL,
  `Title` varchar(255) DEFAULT NULL,
  `Description` varchar(255) DEFAULT NULL,
  `Author` varchar(100) DEFAULT NULL,
  `AuthorID` varchar(40) DEFAULT NULL,
  `URL` varchar(100) DEFAULT NULL,
  `FeaturedImageAssetID` varchar(40) DEFAULT NULL,
  `SubscriptionLevel` int(11) DEFAULT NULL,
  `HtmlBody` text DEFAULT NULL,
  `AudioAssetID` varchar(40) DEFAULT NULL,
  `IsLiveStream` bit(1) DEFAULT NULL,
  `IsLive` bit(1) DEFAULT NULL,
  `OldContentID` varchar(100) DEFAULT NULL,
  `CreatedOnUTC` datetime DEFAULT NULL,
  `CreatedBy` varchar(40) DEFAULT NULL,
  `ModifiedOnUTC` datetime DEFAULT NULL,
  `ModifiedBy` varchar(40) DEFAULT NULL,
  `PublishOnUTC` datetime DEFAULT NULL,
  `PublishedBy` varchar(40) DEFAULT NULL,
  `AnnounceOnUTC` datetime DEFAULT NULL,
  `AnnouncedBy` varchar(40) DEFAULT NULL,
  `PinnedOnUTC` datetime DEFAULT NULL,
  `PinnedBy` varchar(40) DEFAULT NULL,
  `DeletedOnUTC` datetime DEFAULT NULL,
  `DeletedBy` varchar(40) DEFAULT NULL,
  PRIMARY KEY (`ContentID`),
  UNIQUE KEY `ContentID_UNIQUE` (`ContentID`),
  KEY `Content_Url` (`URL`),
  KEY `Content_Author` (`AuthorID`),
  KEY `Content_Dates` (`PublishOnUTC`,`PinnedOnUTC`,`DeletedOnUTC`),
  KEY `Content_Live` (`IsLiveStream`,`IsLive`,`Type`),
  FULLTEXT KEY `Content_Search` (`Title`,`Description`,`HtmlBody`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Comment_Comment`
--

DROP TABLE IF EXISTS `Comment_Comment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Comment_Comment` (
  `CommentID` varchar(40) NOT NULL,
  `ParentCommentID` varchar(40) DEFAULT NULL,
  `ContentID` varchar(40) NOT NULL,
  `UserID` varchar(40) NOT NULL,
  `CommentText` varchar(1000) NOT NULL,
  `CreatedOnUTC` datetime NOT NULL,
  `CreatedBy` varchar(40) NOT NULL,
  `ModifiedOnUTC` datetime NOT NULL,
  `ModifiedBy` varchar(40) NOT NULL,
  `PinnedOnUTC` datetime DEFAULT NULL,
  `PinnedBy` varchar(40) DEFAULT NULL,
  `DeletedOnUTC` datetime DEFAULT NULL,
  `DeletedBy` varchar(40) DEFAULT NULL,
  PRIMARY KEY (`CommentID`),
  UNIQUE KEY `CommentID_UNIQUE` (`CommentID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Comment_Like`
--

DROP TABLE IF EXISTS `Comment_Like`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Comment_Like` (
  `CommentID` varchar(40) NOT NULL,
  `UserID` varchar(40) NOT NULL,
  `LikedOnUTC` datetime NOT NULL,
  PRIMARY KEY (`CommentID`,`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Payment_Generic_Payment`
--

DROP TABLE IF EXISTS `Payment_Generic_Payment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Payment_Generic_Payment` (
  `InternalPaymentID` varchar(40) NOT NULL,
  `InternalSubscriptionID` varchar(40) NOT NULL,
  `UserID` varchar(40) DEFAULT NULL,
  `ProcessorPaymentID` varchar(40) DEFAULT NULL,
  `Status` tinyint(3) unsigned DEFAULT NULL,
  `AmountCents` int(10) unsigned DEFAULT NULL,
  `TaxCents` int(10) unsigned DEFAULT NULL,
  `TaxRateThousandPercents` int(10) unsigned DEFAULT NULL,
  `TotalCents` int(10) unsigned DEFAULT NULL,
  `CreatedOnUTC` datetime DEFAULT NULL,
  `CreatedBy` varchar(40) DEFAULT NULL,
  `ModifiedOnUTC` datetime DEFAULT NULL,
  `ModifiedBy` varchar(40) DEFAULT NULL,
  `PaidOnUTC` datetime DEFAULT NULL,
  `PaidThruUTC` datetime DEFAULT NULL,
  `OldPaymentID` varchar(45) DEFAULT NULL,
  PRIMARY KEY (`InternalPaymentID`),
  UNIQUE KEY `FortisInternalPaymentID_UNIQUE` (`InternalPaymentID`),
  KEY `UserID_IDX` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Payment_Generic_Subscription`
--

DROP TABLE IF EXISTS `Payment_Generic_Subscription`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Payment_Generic_Subscription` (
  `InternalSubscriptionID` varchar(40) NOT NULL,
  `UserID` varchar(40) DEFAULT NULL,
  `ProcessorName` enum('fortis','paypal','stripe') NOT NULL,
  `ProcessorCustomerID` varchar(40) DEFAULT NULL,
  `ProcessorSubscriptionID` varchar(40) DEFAULT NULL,
  `Status` tinyint(3) unsigned DEFAULT NULL,
  `AmountCents` int(10) unsigned DEFAULT NULL,
  `TaxCents` int(10) unsigned DEFAULT NULL,
  `TaxRateThousandPercents` int(10) unsigned DEFAULT NULL,
  `TotalCents` int(10) unsigned DEFAULT NULL,
  `CreatedOnUTC` datetime DEFAULT NULL,
  `CreatedBy` varchar(40) DEFAULT NULL,
  `ModifiedOnUTC` datetime DEFAULT NULL,
  `ModifiedBy` varchar(40) DEFAULT NULL,
  `CanceledOnUTC` datetime DEFAULT NULL,
  `CanceledBy` varchar(40) DEFAULT NULL,
  `OldSubscriptionID` varchar(45) DEFAULT NULL,
  PRIMARY KEY (`InternalSubscriptionID`),
  UNIQUE KEY `FortisInternalSubscriptionID_UNIQUE` (`InternalSubscriptionID`),
  KEY `UserID_IDX` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Payment_Manual_Subscription`
--

DROP TABLE IF EXISTS `Payment_Manual_Subscription`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Payment_Manual_Subscription` (
  `ManualSubscriptionID` varchar(40) NOT NULL,
  `UserID` varchar(40) DEFAULT NULL,
  `AmountCents` int(10) unsigned DEFAULT NULL,
  `CreatedOnUTC` datetime DEFAULT NULL,
  `CreatedBy` varchar(40) DEFAULT NULL,
  `ModifiedOnUTC` datetime DEFAULT NULL,
  `ModifiedBy` varchar(40) DEFAULT NULL,
  `CanceledOnUTC` datetime DEFAULT NULL,
  `CanceledBy` varchar(40) DEFAULT NULL,
  PRIMARY KEY (`ManualSubscriptionID`),
  UNIQUE KEY `ManualSubscriptionID_UNIQUE` (`ManualSubscriptionID`),
  KEY `UserID_IDX` (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Stats_Content`
--

DROP TABLE IF EXISTS `Stats_Content`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Stats_Content` (
  `ContentID` varchar(40) NOT NULL,
  `Likes` bigint(20) NOT NULL DEFAULT 0,
  `Saves` bigint(20) NOT NULL DEFAULT 0,
  `Shares` bigint(20) NOT NULL DEFAULT 0,
  `Views` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`ContentID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Stats_ContentUser`
--

DROP TABLE IF EXISTS `Stats_ContentUser`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Stats_ContentUser` (
  `ContentID` varchar(40) NOT NULL,
  `UserID` varchar(40) NOT NULL,
  `LikedOnUTC` datetime DEFAULT NULL,
  `SavedOnUTC` datetime DEFAULT NULL,
  `ViewedLastOnUTC` datetime DEFAULT NULL,
  `NumberOfShares` int(11) NOT NULL DEFAULT 0,
  `NumberOfViews` bigint(20) NOT NULL DEFAULT 0,
  `Progress` float DEFAULT NULL,
  `ProgressUpdatedOnUTC` datetime DEFAULT NULL,
  PRIMARY KEY (`ContentID`,`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Stats_Shares`
--

DROP TABLE IF EXISTS `Stats_Shares`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Stats_Shares` (
  `Id` bigint(1) NOT NULL AUTO_INCREMENT,
  `ContentID` varchar(40) NOT NULL,
  `UserID` varchar(40) NOT NULL,
  `SharedOnUTC` datetime NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Stats_User`
--

DROP TABLE IF EXISTS `Stats_User`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Stats_User` (
  `UserID` varchar(40) NOT NULL,
  `Likes` bigint(20) NOT NULL DEFAULT 0,
  `Saves` bigint(20) NOT NULL DEFAULT 0,
  `Shares` bigint(20) NOT NULL DEFAULT 0,
  `Views` bigint(20) NOT NULL DEFAULT 0,
  PRIMARY KEY (`UserID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Stats_Views`
--

DROP TABLE IF EXISTS `Stats_Views`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Stats_Views` (
  `Id` bigint(1) NOT NULL AUTO_INCREMENT,
  `ContentID` varchar(40) NOT NULL,
  `UserID` varchar(40) NOT NULL,
  `ViewedOnUTC` datetime NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=25 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Temporary table structure for view `view_activesubscriptionsbyyearmonth`
--

DROP TABLE IF EXISTS `view_activesubscriptionsbyyearmonth`;
/*!50001 DROP VIEW IF EXISTS `view_activesubscriptionsbyyearmonth`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE VIEW `view_activesubscriptionsbyyearmonth` AS SELECT
 1 AS `subscription_id`,
  1 AS `Year`,
  1 AS `Month` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `view_allyearsandmonths`
--

DROP TABLE IF EXISTS `view_allyearsandmonths`;
/*!50001 DROP VIEW IF EXISTS `view_allyearsandmonths`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE VIEW `view_allyearsandmonths` AS SELECT
 1 AS `Year`,
  1 AS `Month`,
  1 AS `start`,
  1 AS `stop` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `view_inactivesubscriptionsbyyearmonth`
--

DROP TABLE IF EXISTS `view_inactivesubscriptionsbyyearmonth`;
/*!50001 DROP VIEW IF EXISTS `view_inactivesubscriptionsbyyearmonth`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE VIEW `view_inactivesubscriptionsbyyearmonth` AS SELECT
  1 AS `subscription_id`,
  1 AS `Year`,
  1 AS `Month` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `view_membersbystartandstop`
--

DROP TABLE IF EXISTS `view_membersbystartandstop`;
/*!50001 DROP VIEW IF EXISTS `view_membersbystartandstop`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE VIEW `view_membersbystartandstop` AS SELECT
 1 AS `user_id`,
  1 AS `start`,
  1 AS `stop` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `view_membersthatsubscribedatleastonce`
--

DROP TABLE IF EXISTS `view_membersthatsubscribedatleastonce`;
/*!50001 DROP VIEW IF EXISTS `view_membersthatsubscribedatleastonce`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE VIEW `view_membersthatsubscribedatleastonce` AS SELECT
 1 AS `user_id`,
  1 AS `status` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `view_subscriptionsbystartandstop`
--

DROP TABLE IF EXISTS `view_subscriptionsbystartandstop`;
/*!50001 DROP VIEW IF EXISTS `view_subscriptionsbystartandstop`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8;
/*!50001 CREATE VIEW `view_subscriptionsbystartandstop` AS SELECT
 1 AS `subscription_id`,
  1 AS `start`,
  1 AS `stop` */;
SET character_set_client = @saved_cs_client;

--
-- Final view structure for view `view_activesubscriptionsbyyearmonth`
--

/*!50001 DROP VIEW IF EXISTS `view_activesubscriptionsbyyearmonth`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb3 */;
/*!50001 SET character_set_results     = utf8mb3 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
DEFINER=CURRENT_USER SQL SECURITY DEFINER
/*!50001 VIEW `view_activesubscriptionsbyyearmonth` AS select 1 AS `subscription_id`,1 AS `Year`,1 AS `Month` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `view_allyearsandmonths`
--

/*!50001 DROP VIEW IF EXISTS `view_allyearsandmonths`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb3 */;
/*!50001 SET character_set_results     = utf8mb3 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
DEFINER=CURRENT_USER SQL SECURITY DEFINER
/*!50001 VIEW `view_allyearsandmonths` AS select 1 AS `Year`,1 AS `Month`,1 AS `start`,1 AS `stop` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `view_inactivesubscriptionsbyyearmonth`
--

/*!50001 DROP VIEW IF EXISTS `view_inactivesubscriptionsbyyearmonth`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb3 */;
/*!50001 SET character_set_results     = utf8mb3 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
DEFINER=CURRENT_USER SQL SECURITY DEFINER
/*!50001 VIEW `view_inactivesubscriptionsbyyearmonth` AS select 1 AS `subscription_id`,1 AS `Year`,1 AS `Month` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `view_membersbystartandstop`
--

/*!50001 DROP VIEW IF EXISTS `view_membersbystartandstop`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb3 */;
/*!50001 SET character_set_results     = utf8mb3 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
DEFINER=CURRENT_USER SQL SECURITY DEFINER
/*!50001 VIEW `view_membersbystartandstop` AS select 1 AS `user_id`,1 AS `start`,1 AS `stop` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `view_membersthatsubscribedatleastonce`
--

/*!50001 DROP VIEW IF EXISTS `view_membersthatsubscribedatleastonce`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb3 */;
/*!50001 SET character_set_results     = utf8mb3 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
DEFINER=CURRENT_USER SQL SECURITY DEFINER
/*!50001 VIEW `view_membersthatsubscribedatleastonce` AS select 1 AS `user_id`,1 AS `status` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `view_subscriptionsbystartandstop`
--

/*!50001 DROP VIEW IF EXISTS `view_subscriptionsbystartandstop`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb3 */;
/*!50001 SET character_set_results     = utf8mb3 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
DEFINER=CURRENT_USER SQL SECURITY DEFINER
/*!50001 VIEW `view_subscriptionsbystartandstop` AS select 1 AS `subscription_id`,1 AS `start`,1 AS `stop` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*M!100616 SET NOTE_VERBOSITY=@OLD_NOTE_VERBOSITY */;