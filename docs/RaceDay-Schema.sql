-- Create Database
CREATE DATABASE RaceDayDB;
GO
USE RaceDayDB;
GO
-- 1. Users Table
CREATE TABLE Users (
 UserID INT IDENTITY(1,1) PRIMARY KEY,
 Email NVARCHAR(255) UNIQUE NOT NULL,
 PasswordHash NVARCHAR(255) NOT NULL,
 FullName NVARCHAR(100) NOT NULL,
 Role NVARCHAR(20) NOT NULL CHECK (Role IN ('Organiser', 'Participant')),
 CreatedAt DATETIME DEFAULT GETDATE()
);
GO
-- 2. Organisers Table
CREATE TABLE Organisers (
 OrganiserID INT IDENTITY(1,1) PRIMARY KEY,
 UserID INT UNIQUE NOT NULL,
 OrganisationName NVARCHAR(100) NOT NULL,
 ContactNumber NVARCHAR(20) NOT NULL,
 FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE
);
GO
-- 3. Participants Table
CREATE TABLE Participants (
 ParticipantID INT IDENTITY(1,1) PRIMARY KEY,
 UserID INT UNIQUE NOT NULL,
 DateOfBirth DATE NOT NULL,
 ProfilePictureURL NVARCHAR(500) NULL,
 FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE
);
GO
-- 4. Events Table
CREATE TABLE Events (
 EventID INT IDENTITY(1,1) PRIMARY KEY,
 OrganiserID INT NOT NULL,
 Name NVARCHAR(100) NOT NULL,
 Description NVARCHAR(500) NOT NULL,
 EventDate DATETIME NOT NULL,
 Location NVARCHAR(200) NOT NULL,
 Distance DECIMAL(5,2) NOT NULL,
 EventType NVARCHAR(20) NOT NULL CHECK (EventType IN ('Run', 'Walk', 'Cycle')),

 BannerImageURL NVARCHAR(500) NULL,
 CreatedAt DATETIME DEFAULT GETDATE(),
 FOREIGN KEY (OrganiserID) REFERENCES Organisers(OrganiserID)
);
GO
-- 5. Categories Table
CREATE TABLE Categories (
 CategoryID INT IDENTITY(1,1) PRIMARY KEY,
 EventID INT NOT NULL,
 Name NVARCHAR(50) NOT NULL,
 Description NVARCHAR(200) NULL,
 MinAge INT NULL,
 MaxAge INT NULL,
 FOREIGN KEY (EventID) REFERENCES Events(EventID) ON DELETE CASCADE
);
GO
-- 6. Enrolments Table
CREATE TABLE Enrolments (
 EnrolmentID INT IDENTITY(1,1) PRIMARY KEY,
 ParticipantID INT NOT NULL,
 EventID INT NOT NULL,
 CategoryID INT NOT NULL,
 RegistrationDate DATETIME DEFAULT GETDATE(),
 Status NVARCHAR(20) DEFAULT 'Pending' CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled')),
 CONSTRAINT UQ_Enrolment UNIQUE (ParticipantID, EventID),
 FOREIGN KEY (ParticipantID) REFERENCES Participants(ParticipantID),
 FOREIGN KEY (EventID) REFERENCES Events(EventID),
 FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID)
);
GO
-- 7. Results Table
CREATE TABLE Results (
 ResultID INT IDENTITY(1,1) PRIMARY KEY,
 EnrolmentID INT UNIQUE NOT NULL,
 FinishTime TIME NOT NULL,
 FinishPosition INT NOT NULL,
 Status NVARCHAR(20) DEFAULT 'Published' CHECK (Status IN ('Published', 'Pending', 'Disqualified')),
 FOREIGN KEY (EnrolmentID) REFERENCES Enrolments(EnrolmentID)
);
GO
-- Seed Users
INSERT INTO Users (Email, PasswordHash, FullName, Role) VALUES
('organiser1@raceday.com', 'hashed_password_123', 'John Organiser', 'Organiser'),
('organiser2@raceday.com', 'hashed_password_456', 'Sarah Events', 'Organiser'),
('participant1@raceday.com', 'hashed_password_789', 'Tom Runner', 'Participant'),
('participant2@raceday.com', 'hashed_password_abc', 'Lisa Walker', 'Participant'),
('participant3@raceday.com', 'hashed_password_def', 'Mike Cyclist', 'Participant');
GO
-- Seed Organisers
INSERT INTO Organisers (UserID, OrganisationName, ContactNumber) VALUES
(1, 'Cape Town Events', '+27 82 123 4567'),
(2, 'Durban Sports', '+27 83 987 6543');
GO
-- Seed Participants
INSERT INTO Participants (UserID, DateOfBirth, ProfilePictureURL) VALUES
(3, '1990-05-15', NULL),
(4, '1985-10-20', NULL),
(5, '1995-03-08', NULL);
GO
-- Seed Events
INSERT INTO Events (OrganiserID, Name, Description, EventDate, Location, Distance, EventType) VALUES
(1, 'Cape Town Cycle Tour', 'Annual cycling event through Cape Town', '2026-03-15 07:00:00', 'Cape Town, Western Cape', 109.00, 'Cycle'),
(1, 'Soweto Marathon', 'Iconic marathon through Soweto', '2026-04-20 06:30:00', 'Soweto, Gauteng', 42.20, 'Run'),
(2, 'Durban Walkathon', 'Scenic coastal walk for charity', '2026-05-10 08:00:00', 'Durban, KwaZulu-Natal', 21.10, 'Walk');
GO
-- Seed Categories
INSERT INTO Categories (EventID, Name, Description, MinAge, MaxAge) VALUES
(1, 'Under 20', 'Cyclists under 20 years old', 15, 19),
(1, 'Senior', 'Cyclists 20-39 years old', 20, 39),
(1, 'Veteran', 'Cyclists 40+ years old', 40, NULL),
(2, 'Under 20', 'Runners under 20', 15, 19),
(2, 'Senior', 'Runners 20-39 years old', 20, 39),
(2, 'Veteran', 'Runners 40+ years old', 40, NULL),
(3, 'Under 20', 'Walkers under 20', 15, 19),
(3, 'Senior', 'Walkers 20-39 years old', 20, 39),
(3, 'Veteran', 'Walkers 40+ years old', 40, NULL);
GO
-- Seed Enrolments
INSERT INTO Enrolments (ParticipantID, EventID, CategoryID, Status) VALUES
(1, 1, 2, 'Confirmed'),
(1, 2, 5, 'Pending'),
(2, 1, 3, 'Confirmed'),
(2, 3, 8, 'Confirmed'),
(3, 1, 1, 'Pending'),
(3, 3, 9, 'Confirmed');
GO
-- Seed Results
INSERT INTO Results (EnrolmentID, FinishTime, FinishPosition) VALUES
(1, '03:45:30', 47),
(3, '04:12:15', 78),
(4, '02:30:45', 12),
(6, '03:55:20', 34);
GO
