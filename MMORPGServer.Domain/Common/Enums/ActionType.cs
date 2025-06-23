namespace MMORPGServer.Domain.Common.Enums
{
    /// <summary>
    /// Defines all possible action types that can be performed in the game.
    /// </summary>
    public enum ActionType : short
    {
        #region Movement and Position
        /// <summary>
        /// Teleport to a specific location
        /// </summary>
        SetLocation = 74,

        /// <summary>
        /// Change the character's facing direction
        /// </summary>
        ChangeDirection = 79,

        /// <summary>
        /// Change the character's stance
        /// </summary>
        ChangeStance = 81,

        /// <summary>
        /// Change to a different map
        /// </summary>
        ChangeMap = 85,

        /// <summary>
        /// Teleport to a specific location
        /// </summary>
        Teleport = 86,

        /// <summary>
        /// Teleport back to previous location
        /// </summary>
        TeleportBack = 108,

        /// <summary>
        /// Jump to a location
        /// </summary>
        Jump = 137,

        /// <summary>
        /// Flash step movement
        /// </summary>
        FlashStep = 156,

        /// <summary>
        /// Space leap movement
        /// </summary>
        SpaceLeap = 451,
        #endregion

        #region Character State
        /// <summary>
        /// Character has leveled up
        /// </summary>
        Leveled = 92,

        /// <summary>
        /// Revive the character
        /// </summary>
        Revive = 94,

        /// <summary>
        /// Delete the character
        /// </summary>
        DeleteCharacter = 95,

        /// <summary>
        /// Set PK mode
        /// </summary>
        SetPkMode = 96,

        /// <summary>
        /// Set character as away
        /// </summary>
        Away = 161,

        /// <summary>
        /// Set character appearance type
        /// </summary>
        SetAppearanceType = 178,

        /// <summary>
        /// Change character's lookface
        /// </summary>
        ChangeLookface = 151,

        /// <summary>
        /// Change character's face
        /// </summary>
        ChangeFace = 151,
        #endregion

        #region Skills and Magic
        /// <summary>
        /// Update spell information
        /// </summary>
        UpdateSpell = 252,

        /// <summary>
        /// Remove a spell
        /// </summary>
        RemoveSpell = 109,

        /// <summary>
        /// Abort current magic
        /// </summary>
        AbortMagic = 163,

        /// <summary>
        /// Update profession information
        /// </summary>
        UpdateProf = 253,
        #endregion

        #region Inventory and Equipment
        /// <summary>
        /// View equipment information
        /// </summary>
        ViewEquipment = 117,

        /// <summary>
        /// Query equipment information
        /// </summary>
        QueryEquipment = 408,

        /// <summary>
        /// Update inventory sash
        /// </summary>
        UpdateInventorySash = 256,
        #endregion

        #region Trading and Economy
        /// <summary>
        /// Start vending
        /// </summary>
        StartVendor = 111,

        /// <summary>
        /// Stop vending
        /// </summary>
        StopVending = 114,

        /// <summary>
        /// View trade partner information
        /// </summary>
        TradePartnerInfo = 152,

        /// <summary>
        /// Submit gold brick
        /// </summary>
        SubmitGoldBrick = 436,

        /// <summary>
        /// Place bet on poker table
        /// </summary>
        PoketTableBet = 234,
        #endregion

        #region Social and Communication
        /// <summary>
        /// Confirm guild information
        /// </summary>
        ConfirmGuild = 97,

        /// <summary>
        /// Team search for member
        /// </summary>
        TeamSearchForMember = 106,

        /// <summary>
        /// Location of team leader
        /// </summary>
        LocationTeamLieder = 101,

        /// <summary>
        /// Add to blacklist
        /// </summary>
        AddBlackList = 440,

        /// <summary>
        /// Remove from blacklist
        /// </summary>
        RemoveBlackList = 441,

        /// <summary>
        /// View friend information
        /// </summary>
        ViewFriendInfo = 148,

        /// <summary>
        /// View enemy information
        /// </summary>
        ViewEnemyInfo = 123,
        #endregion

        #region UI and Interface
        /// <summary>
        /// Set hotkeys
        /// </summary>
        Hotkeys = 75,

        /// <summary>
        /// Confirm associates
        /// </summary>
        ConfirmAssociates = 76,

        /// <summary>
        /// Confirm proficiencies
        /// </summary>
        ConfirmProficiencies = 77,

        /// <summary>
        /// Confirm spells
        /// </summary>
        ConfirmSpells = 78,

        /// <summary>
        /// Open GUI NPC
        /// </summary>
        OpenGuiNpc = 160,

        /// <summary>
        /// Open custom interface
        /// </summary>
        OpenCustom = 116,

        /// <summary>
        /// Open dialog
        /// </summary>
        OpenDialog = 126,

        /// <summary>
        /// Set map color
        /// </summary>
        SetMapColor = 104,

        /// <summary>
        /// Display bulletin
        /// </summary>
        Bulletin = 166,

        /// <summary>
        /// Display countdown
        /// </summary>
        CountDown = 159,
        #endregion

        #region Game Mechanics
        /// <summary>
        /// Complete login process
        /// </summary>
        CompleteLogin = 132,

        /// <summary>
        /// Revive monster
        /// </summary>
        ReviveMonster = 134,

        /// <summary>
        /// Remove entity
        /// </summary>
        RemoveEntity = 135,

        /// <summary>
        /// Request entity information
        /// </summary>
        RequestEntity = 102,

        /// <summary>
        /// Query spawn information
        /// </summary>
        QuerySpawn = 310,

        /// <summary>
        /// Remove trap
        /// </summary>
        RemoveTrap = 434,

        /// <summary>
        /// Pick up item
        /// </summary>
        Pick = 164,

        /// <summary>
        /// End transformation
        /// </summary>
        EndTransformation = 118,

        /// <summary>
        /// End flying
        /// </summary>
        EndFly = 120,

        /// <summary>
        /// Set ghost mode
        /// </summary>
        Ghost = 145,
        #endregion

        #region Special Features
        /// <summary>
        /// Auto patcher
        /// </summary>
        AutoPatcher = 162,

        /// <summary>
        /// Dragon ball
        /// </summary>
        DragonBall = 165,

        /// <summary>
        /// Poker teleporter
        /// </summary>
        PokerTeleporter = 167,

        /// <summary>
        /// Begin steed race
        /// </summary>
        BeginSteedRace = 401,

        /// <summary>
        /// Finish steed race
        /// </summary>
        FinishSteedRace = 402,

        /// <summary>
        /// Draw story
        /// </summary>
        DrawStory = 443,

        /// <summary>
        /// Ninja story of eight gates
        /// </summary>
        NinjaStoryOfEghitGate = 456,

        /// <summary>
        /// Pet attack
        /// </summary>
        PetAttack = 447,

        /// <summary>
        /// Allow animation
        /// </summary>
        AllowAnimation = 251,

        /// <summary>
        /// Credit gifts
        /// </summary>
        CreditGifts = 255,

        /// <summary>
        /// Cliker ON
        /// </summary>
        ClikerON = 171,

        /// <summary>
        /// Cliker entry
        /// </summary>
        ClikerEntry = 172,
        #endregion
    }
}