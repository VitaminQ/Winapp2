﻿Option Strict On
Imports System.IO

Public Module Trim

    Dim saveDir As String = Environment.CurrentDirectory
    Dim saveName As String = "\winapp2.ini"
    Dim winappName As String = "\winapp2.ini"
    Dim winappDir As String = Environment.CurrentDirectory
    Dim winVer As Double = getWinVer()
    Dim detChrome As New List(Of String)

    Dim hasMenuTopper As Boolean = False
    Dim exitCode As Boolean = False

    Private Sub printmenu()
        printMenuLine(tmenu("Trim"))
        printMenuLine(menuStr03)
        printMenuLine("This tool will trim winapp2.ini such that it contains only entries relevant to your machine,", "c")
        printMenuLine("greatly reducing both application load time and the winapp2.ini file size.", "c")
        printMenuLine(menuStr04)
        printMenuLine("0. Exit               - Return to the winapp2ool menu", "l")
        printMenuLine("1. Run (default)      - Trim winapp2.ini and overwrite the existing file", "l")
        printMenuLine("2. Run (custom)       - Change the save directory and/or file name", "l")
        printMenuLine("3. Run (download)     - Download and trim the latest winapp2.ini and save it to the current folder", "l")
        printMenuLine(menuStr02)
    End Sub

    Public Sub main()
        exitCode = False
        hasMenuTopper = False
        Console.Clear()

        Dim input As String
        Do Until exitCode
            printmenu()
            Console.WriteLine()
            Console.Write("Enter a number, or leave blank to run the default: ")
            input = Console.ReadLine()

            Select Case input
                Case "0"
                    Console.WriteLine("Returning to winapp2ool menu...")
                    exitCode = True
                Case "1", ""
                    initTrim()
                Case "2"
                    fChooser(saveDir, saveName, exitCode, "\winapp2.ini", "\winapp2-trimmed.ini")
                    initTrim()
                Case "3"
                    initDownloadedTrim(Downloader.wa2Link, "\winapp2.ini")
                Case Else
                    Console.Write("Invalid input. Please try again: ")
                    input = Console.ReadLine
            End Select
        Loop
    End Sub

    Public Sub remoteTrim(wd As String, wn As String, sd As String, sn As String, download As Boolean, ncc As Boolean)
        'Handle input taken from the command line to initiate a trim
        saveDir = sd
        saveName = sn
        winappDir = wd
        winappName = wn
        If download Then
            Dim link As String = If(ncc, nonccLink, wa2Link)
            initDownloadedTrim(link, "\winapp2.ini")
        Else
            initTrim()
        End If
    End Sub

    Private Sub initTrim()
        'Validate winapp2.ini's existence, load it as an inifile and convert it to a winapp2file before passing it to trim
        Dim winappfile As iniFile = validate(winappDir, winappName, exitCode, "\winapp2.ini", "\winapp2-2.ini")
        If exitCode Then Exit Sub
        Dim winapp2 As New winapp2file(winappfile)
        trim(winapp2)
        revertMenu(exitCode)
        Console.Clear()
    End Sub


    Public Sub initDownloadedTrim(link As String, name As String)
        'Grab a remote ini file and toss it into the trimmer
        trim(New winapp2file(getRemoteIniFile(link, name)))
        revertMenu(exitCode)
    End Sub

    Private Sub trim(winapp2 As winapp2file)
        Console.Clear()
        printMenuLine("Trimming...", "l")

        detChrome.AddRange(New String() {"%AppData%\ChromePlus\chrome.exe", "%LocalAppData%\Chromium\Application\chrome.exe", "%LocalAppData%\Chromium\chrome.exe", "%LocalAppData%\Flock\Application\flock.exe", "%LocalAppData%\Google\Chrome SxS\Application\chrome.exe",
                           "%LocalAppData%\Google\Chrome\Application\chrome.exe", "%LocalAppData%\RockMelt\Application\rockmelt.exe", "%LocalAppData%\SRWare Iron\iron.exe", "%ProgramFiles%\Chromium\Application\chrome.exe", "%ProgramFiles%\SRWare Iron\iron.exe",
                           "%ProgramFiles%\Chromium\chrome.exe", "%ProgramFiles%\Flock\Application\flock.exe", "%ProgramFiles%\Google\Chrome SxS\Application\chrome.exe", "%ProgramFiles%\Google\Chrome\Application\chrome.exe", "%ProgramFiles%\RockMelt\Application\rockmelt.exe",
                           "HKCU\Software\Chromium", "HKCU\Software\SuperBird", "HKCU\Software\Torch", "HKCU\Software\Vivaldi"})


        'Save our inital # of entries
        Dim entryCountBeforeTrim As Integer = winapp2.count

        'Process our entries
        processEntryList(winapp2.cEntriesW)
        processEntryList(winapp2.fxEntriesW)
        processEntryList(winapp2.tbEntriesW)
        processEntryList(winapp2.mEntriesW)

        'Update the internal inifile objects
        winapp2.rebuildToIniFiles()

        'Sort the file so that entries are written back alphabetically
        winapp2.sortInneriniFiles()

        'Print out the results from the trim
        printMenuLine("...done.", "l")
        Console.Clear()
        printMenuLine(tmenu("Trim Complete"))
        printMenuLine(moMenu("Results"))
        printMenuLine(menuStr03)
        printMenuLine("Number of entries before trimming: " & entryCountBeforeTrim, "l")
        printMenuLine("Number of entries after trimming: " & winapp2.count, "l")
        printMenuLine(menuStr01)
        printMenuLine("Press any key to return to the winapp2ool menu", "l")
        printMenuLine(menuStr02)

        Try
            'Rewrite our rebuilt ini back to disk
            Dim file As New StreamWriter(saveDir & saveName, False)

            file.Write(winapp2.winapp2string())
            file.Close()
        Catch ex As Exception
            exc(ex)
        Finally
            If Not suppressOutput Then Console.ReadKey()
        End Try
    End Sub

    Private Function processEntryExistence(ByRef entry As winapp2entry) As Boolean
        'Returns true if an entry would be detected by CCleaner on parse, returns false otherwise

        Dim hasDetOS As Boolean = Not entry.detectOS.Count = 0
        Dim hasMetDetOS As Boolean = False
        Dim exists As Boolean = False

        'Process the DetectOS if we have one, take note if we meet the criteria, otherwise return false
        If hasDetOS Then
            hasMetDetOS = detOSCheck(entry.detectOS(0).value)
            If Not hasMetDetOS Then Return False 
        End If

        'Process our SpecialDetect if we have one
        If Not entry.specialDetect.Count = 0 Then Return checkSpecialDetects(entry.specialDetect(0))

        'Process our Detects
        For Each key In entry.detects
            If checkRegExist(key.value) Then Return True
        Next

        'Process our DetectFiles
        For Each key In entry.detectFiles
            If checkFileExist(key.value) Then Return True
        Next

        'Return true for the special case where we have only a DetectOS and we meet its criteria 
        If hasMetDetOS And entry.specialDetect.Count = 0 And entry.detectFiles.Count = 0 And entry.detects.Count = 0 Then Return True

        Return False
    End Function

    Private Sub processEntryList(ByRef entryList As List(Of winapp2entry))
        'Processes a list of winapp2.ini entries and removes any from the list that wouldn't be detected by CCleaner

        Dim sectionsToBePruned As New List(Of winapp2entry)
        For Each entry In entryList
            If Not processEntryExistence(entry) Then sectionsToBePruned.Add(entry)
        Next

        removeEntries(entryList, sectionsToBePruned)
    End Sub

    Private Function checkSpecialDetects(ByVal key As iniKey) As Boolean
        'Return true if a SpecialDetect location exists

        Select Case key.value
            Case "DET_CHROME"
                For Each path As String In detChrome
                    If checkExist(path) Then Return True
                Next
            Case "DET_MOZILLA"
                Return checkFileExist("%AppData%\Mozilla\Firefox")
            Case "DET_THUNDERBIRD"
                Return checkFileExist("%AppData%\Thunderbird")
            Case "DET_OPERA"
                Return checkFileExist("%AppData%\Opera Software")
        End Select

        'If we didn't return above, SpecialDetect definitely doesn't exist
        Return False
    End Function

    Private Function checkExist(key As String) As Boolean
        'The only remaining use for this is the DET_CHROME which has a mix of file and registry detections 
        Return If(key.StartsWith("HK"), checkRegExist(key), checkFileExist(key))
    End Function

    Private Function checkRegExist(key As String) As Boolean
        'Returns True if a given Detect key exists on the system, otherwise returns False

        Dim dir As String = key
        Dim splitDir As String() = dir.Split(CChar("\"))

        Try
            'splitDir(0) sould contain the registry hive location, remove it and query the hive for existence
            Select Case splitDir(0)
                Case "HKCU"
                    dir = dir.Replace("HKCU\", "")
                    Return Microsoft.Win32.Registry.CurrentUser.OpenSubKey(dir, True) IsNot Nothing
                Case "HKLM"
                    dir = dir.Replace("HKLM\", "")
                    If Microsoft.Win32.Registry.LocalMachine.OpenSubKey(dir, True) IsNot Nothing Then
                        Return True
                    Else
                        Dim rDir As String = dir.ToLower.Replace("software\", "Software\WOW6432Node\")
                        Return Microsoft.Win32.Registry.LocalMachine.OpenSubKey(rDir, True) IsNot Nothing
                    End If
                Case "HKU"
                    dir = dir.Replace("HKU\", "")
                    Return Microsoft.Win32.Registry.Users.OpenSubKey(dir, True) IsNot Nothing
                Case "HKCR"
                    dir = dir.Replace("HKCR\", "")
                    Return Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(dir, True) IsNot Nothing
            End Select
        Catch ex As Exception
            'The most common (only?) exception here is a permissions one, so assume true if we hit
            'because a permissions exception implies the key exists anyway.
            Return True
        End Try
        'If we didn't return anything above, registry location probably doesn't exist
        Return False
    End Function

    Private Function checkFileExist(key As String) As Boolean
        'Returns True if a DetectFile path exists on the system, otherwise returns False
        Dim isProgramFiles As Boolean = False
        Dim dir As String = key

        'make sure we get the proper path for environment variables
        If dir.Contains("%") Then
            Dim splitDir As String() = dir.Split(CChar("%"))
            Dim var As String = splitDir(1)
            Dim envDir As String = Environment.GetEnvironmentVariable(var)
            Select Case var
                Case "ProgramFiles"
                    isProgramFiles = True
                Case "Documents"
                    envDir = Environment.GetEnvironmentVariable("UserProfile") & "\Documents"
                Case "CommonAppData"
                    envDir = Environment.GetEnvironmentVariable("ProgramData")
            End Select
            dir = envDir + splitDir(2)
        End If

        Try
            'check out those file/folder paths
            If Directory.Exists(dir) Or File.Exists(dir) Then Return True

            'if we didn't find it and we're looking in Program Files, check the (x86) directory
            If isProgramFiles Then
                Dim envDir As String = Environment.GetEnvironmentVariable("ProgramFiles(x86)")
                dir = envDir & key.Split(CChar("%"))(2)
                Return (Directory.Exists(dir) Or File.Exists(dir))
            End If

        Catch ex As Exception
            exc(ex)
            Return True
        End Try

        Return False
    End Function

    Private Function detOSCheck(value As String) As Boolean
        'Return True if we satisfy the DetectOS criteria, otherwise return False

        Dim splitKey As String() = value.Split(CChar("|"))
        Return If(value.StartsWith("|"), Not winVer > Double.Parse(splitKey(1)), Not winVer < Double.Parse(splitKey(0)))
    End Function

    Private Function getWinVer() As Double
        'Returns the Windows Version mumber, 0.0 if it cannot determine the proper number.

        Dim winver As String = My.Computer.Info.OSFullName.ToString
        Select Case True
            Case winver.Contains("XP")
                Return 5.1
            Case winver.Contains("Vista")
                Return 6.0
            Case winver.Contains("7")
                Return 6.1
            Case winver.Contains("8") And Not winver.Contains("8.1")
                Return 6.2
            Case winver.Contains("8.1")
                Return 6.3
            Case winver.Contains("10")
                Return 10.0
            Case Else
                Console.WriteLine("Unable to determine which version of Windows you are running.")
                Console.WriteLine()
                Return 0.0
        End Select
    End Function
End Module