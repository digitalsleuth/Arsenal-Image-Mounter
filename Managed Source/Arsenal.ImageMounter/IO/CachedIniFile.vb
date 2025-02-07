﻿Imports System.Buffers
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.Versioning
Imports System.Text
Imports Arsenal.ImageMounter.Extensions

Namespace IO

    ''' <summary>
    ''' Class that caches a text INI file
    ''' </summary>
    <ComVisible(False)>
    Public Class CachedIniFile
        Inherits NullSafeDictionary(Of String, NullSafeDictionary(Of String, String))

        Protected Overrides Function GetDefaultValue(Key As String) As NullSafeDictionary(Of String, String)
            Dim new_section As New NullSafeStringDictionary(StringComparer.CurrentCultureIgnoreCase)
            Add(Key, new_section)
            Return new_section
        End Function

        ''' <summary>
        ''' Flushes registry mapping for all INI files.
        ''' is thrown.
        ''' </summary>
        <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
        Public Shared Sub Flush()
            NativeFileIO.UnsafeNativeMethods.WritePrivateProfileStringW(Nothing, Nothing, Nothing, Nothing)
        End Sub

        <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
        Public Shared Iterator Function EnumerateFileSectionNames(filename As ReadOnlyMemory(Of Char)) As IEnumerable(Of String)

            Const NamesSize = 32767

            Dim sectionnames = ArrayPool(Of Char).Shared.Rent(NamesSize)
            Try
                Dim size = NativeFileIO.UnsafeNativeMethods.GetPrivateProfileSectionNamesW(sectionnames(0),
                                                                                           NamesSize,
                                                                                           filename.MakeNullTerminated())

                For Each name In sectionnames.AsMemory(0, size).ParseDoubleTerminatedString()
                    Yield name.ToString()
                Next

            Finally
                ArrayPool(Of Char).Shared.Return(sectionnames)

            End Try

        End Function

        <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
        Public Shared Iterator Function EnumerateFileSectionValuePairs(filename As ReadOnlyMemory(Of Char), section As ReadOnlyMemory(Of Char)) As IEnumerable(Of KeyValuePair(Of String, String))

            Const ValuesSize = 32767

            Dim valuepairs = ArrayPool(Of Char).Shared.Rent(ValuesSize)
            Try
                Dim size = NativeFileIO.UnsafeNativeMethods.GetPrivateProfileSectionW(section.MakeNullTerminated(),
                                                                                      valuepairs(0),
                                                                                      ValuesSize,
                                                                                      filename.MakeNullTerminated())

                For Each valuepair In valuepairs.AsMemory(0, size).ParseDoubleTerminatedString()

                    Dim pos = valuepair.Span.IndexOf("="c)

                    If pos < 0 Then
                        Continue For
                    End If

                    Dim key = valuepair.Slice(0, pos).ToString()
                    Dim value = valuepair.Slice(pos + 1).ToString()

                    Yield New KeyValuePair(Of String, String)(key, value)

                Next

            Finally
                ArrayPool(Of Char).Shared.Return(valuepairs)

            End Try

        End Function

        ''' <summary>
        ''' Saves a value to an INI file by calling Win32 API function WritePrivateProfileString. If call fails and exception
        ''' is thrown.
        ''' </summary>
        ''' <param name="FileName">Name and path of INI file where to save value</param>
        ''' <param name="SectionName">Name of INI file section where to save value</param>
        ''' <param name="SettingName">Name of value to save</param>
        ''' <param name="Value">Value to save</param>
        <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
        Public Shared Sub SaveValue(FileName As ReadOnlyMemory(Of Char), SectionName As ReadOnlyMemory(Of Char), SettingName As ReadOnlyMemory(Of Char), Value As ReadOnlyMemory(Of Char))
            NativeFileIO.Win32Try(NativeFileIO.UnsafeNativeMethods.WritePrivateProfileStringW(SectionName.MakeNullTerminated(),
                                                                                              SettingName.MakeNullTerminated(),
                                                                                              Value.MakeNullTerminated(),
                                                                                              FileName.MakeNullTerminated()))
        End Sub

        ''' <summary>
        ''' Saves a value to an INI file by calling Win32 API function WritePrivateProfileString. If call fails and exception
        ''' is thrown.
        ''' </summary>
        ''' <param name="FileName">Name and path of INI file where to save value</param>
        ''' <param name="SectionName">Name of INI file section where to save value</param>
        ''' <param name="SettingName">Name of value to save</param>
        ''' <param name="Value">Value to save</param>
        <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
        Public Shared Sub SaveValue(FileName As String, SectionName As String, SettingName As String, Value As String)
            NativeFileIO.Win32Try(NativeFileIO.UnsafeNativeMethods.WritePrivateProfileStringW(MemoryMarshal.GetReference(SectionName.AsSpan()),
                                                                                              MemoryMarshal.GetReference(SettingName.AsSpan()),
                                                                                              MemoryMarshal.GetReference(Value.AsSpan()),
                                                                                              MemoryMarshal.GetReference(FileName.AsSpan())))
        End Sub

        ''' <summary>
        ''' Saves a current value from this object to an INI file by calling Win32 API function WritePrivateProfileString.
        ''' If call fails and exception is thrown.
        ''' </summary>
        ''' <param name="FileName">Name and path of INI file where to save value</param>
        ''' <param name="SectionName">Name of INI file section where to save value</param>
        ''' <param name="SettingName">Name of value to save</param>
        <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
        Public Sub SaveValue(FileName As ReadOnlyMemory(Of Char), SectionName As String, SettingName As String)
            SaveValue(SectionName.AsMemory(), SettingName.AsMemory(), Item(SectionName)(SettingName).AsMemory(), FileName)
        End Sub

        ''' <summary>
        ''' Saves a current value from this object to INI file that this object last loaded values from, either through constructor
        ''' call with filename parameter or by calling Load method with filename parameter.
        ''' Operation is carried out by calling Win32 API function WritePrivateProfileString.
        ''' If call fails and exception is thrown.
        ''' </summary>
        ''' <param name="SectionName">Name of INI file section where to save value</param>
        ''' <param name="SettingName">Name of value to save</param>
        <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
        Public Sub SaveValue(SectionName As String, SettingName As String)
            If String.IsNullOrEmpty(_Filename) Then
                Throw New InvalidOperationException("Filename property not set on this object.")
            End If
            SaveValue(SectionName, SettingName, Item(SectionName)(SettingName), _Filename)
        End Sub

        ''' <summary>
        ''' Saves current contents of this object to INI file that this object last loaded values from, either through constructor
        ''' call with filename parameter or by calling Load method with filename parameter.
        ''' </summary>
        Public Sub Save()
            File.WriteAllText(_Filename, ToString(), _Encoding)
        End Sub

        ''' <summary>
        ''' Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
        ''' </summary>
        Public Sub Save(Filename As String, Encoding As Encoding)
            File.WriteAllText(Filename, ToString(), Encoding)
        End Sub

        ''' <summary>
        ''' Saves current contents of this object to an INI file. If the file already exists, it is overwritten.
        ''' </summary>
        Public Sub Save(Filename As String)
            File.WriteAllText(Filename, ToString(), _Encoding)
        End Sub

        Public Overrides Function ToString() As String
            Using Writer As New StringWriter
                WriteTo(Writer)
                Return Writer.ToString()
            End Using
        End Function

        Public Sub WriteTo(Stream As Stream)
            WriteTo(New StreamWriter(Stream, _Encoding) With {.AutoFlush = True})
        End Sub

        Public Sub WriteTo(Writer As TextWriter)
            WriteSectionTo(String.Empty, Writer)
            For Each SectionKey In Keys
                If String.IsNullOrEmpty(SectionKey) Then
                    Continue For
                End If

                WriteSectionTo(SectionKey, Writer)
            Next
            Writer.Flush()
        End Sub

        Public Sub WriteSectionTo(SectionKey As String, Writer As TextWriter)
            If Not ContainsKey(SectionKey) Then
                Return
            End If

            Dim Section = Item(SectionKey)

            Dim any_written = False

            If Not String.IsNullOrEmpty(SectionKey) Then
                Writer.WriteLine($"[{SectionKey}]")
                any_written = True
            End If

            For Each key In Section.Keys.OfType(Of String)()
                Writer.WriteLine($"{key}={Section(key)}")
                any_written = True
            Next

            If any_written Then
                Writer.WriteLine()
            End If
        End Sub

        ''' <summary>
        ''' Name of last INI file loaded into this object.
        ''' </summary>
        Public ReadOnly Property Filename As String

        ''' <summary>
        ''' Text encoding of last INI file loaded into this object.
        ''' </summary>
        Public ReadOnly Property Encoding() As Encoding

        ''' <summary>
        ''' Creates a new empty CachedIniFile object
        ''' </summary>
        Public Sub New()
            MyBase.New(StringComparer.CurrentCultureIgnoreCase)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Filename">Name of INI file to read into the created object</param>
        ''' <param name="Encoding">Text encoding used in INI file</param>
        Public Sub New(Filename As String, Encoding As Encoding)
            Me.New()

            Load(Filename, Encoding)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Filename">Name of INI file to read into the created object</param>
        Public Sub New(Filename As String)
            Me.New(Filename, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Stream">Stream that contains INI settings to read into the created object</param>
        ''' <param name="Encoding">Text encoding used in INI file</param>
        Public Sub New(Stream As Stream, Encoding As Encoding)
            Me.New()

            Load(Stream, Encoding)
        End Sub

        ''' <summary>
        ''' Creates a new CachedIniFile object and fills it with the contents of the specified
        ''' INI file
        ''' </summary>
        ''' <param name="Stream">Stream that contains INI settings to read into the created object</param>
        Public Sub New(Stream As Stream)
            Me.New(Stream, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Reloads settings from disk file. This is only supported if this object was created using
        ''' a constructor that takes a filename or if a Load() method that takes a filename has been
        ''' called earlier.
        ''' </summary>
        Public Sub Reload()
            Load(_Filename, _Encoding)
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Filename">INI file to load</param>
        Public Sub Load(Filename As String)
            Load(Filename, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Filename">INI file to load</param>
        ''' <param name="Encoding">Text encoding for INI file</param>
        Public Sub Load(Filename As String, Encoding As Encoding)
            _Filename = Filename
            _Encoding = Encoding

            Try
                Using fs As New FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 20480, FileOptions.SequentialScan)
                    Load(fs, Encoding)
                End Using

            Catch

            End Try
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Stream">Stream containing INI file data</param>
        ''' <param name="Encoding">Text encoding for INI stream</param>
        Public Sub Load(Stream As Stream, Encoding As Encoding)
            Try
                Dim sr As New StreamReader(Stream, Encoding, False, 1048576)

                Load(sr)

                _Encoding = Encoding

            Catch

            End Try
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object using Default text
        ''' encoding. Existing settings in object is replaced.
        ''' </summary>
        ''' <param name="Stream">Stream containing INI file data</param>
        Public Sub Load(Stream As Stream)
            Load(Stream, Encoding.Default)
        End Sub

        ''' <summary>
        ''' Loads settings from an INI file into this CachedIniFile object. Existing settings
        ''' in object is replaced.
        ''' </summary>
        ''' <param name="Stream">Stream containing INI file data</param>
        Public Sub Load(Stream As TextReader)
            SyncLock SyncRoot
                Try
                    With Stream
                        Dim CurrentSection = Item(String.Empty)

                        Do
                            Dim Linestr = .ReadLine()

                            If Linestr Is Nothing Then
                                Exit Do
                            End If

                            Dim Line = Linestr.AsSpan().Trim()

                            If Line.Length = 0 OrElse Line.StartsWith(";".AsSpan(), StringComparison.Ordinal) Then
                                Continue Do
                            End If

                            If Line.StartsWith("[".AsSpan(), StringComparison.Ordinal) AndAlso Line.EndsWith("]".AsSpan(), StringComparison.Ordinal) Then
                                Dim SectionKey = Line.Slice(1, Line.Length - 2).Trim().ToString()
                                CurrentSection = Item(SectionKey)
                                Continue Do
                            End If

                            Dim EqualSignPos = Line.IndexOf("="c)
                            If EqualSignPos < 0 Then
                                Continue Do
                            End If

                            Dim Key = Line.Slice(0, EqualSignPos).Trim().ToString()
                            Dim Value = Line.Slice(EqualSignPos + 1).Trim().ToString()

                            CurrentSection(Key) = Value

                        Loop
                    End With

                Catch

                End Try
            End SyncLock
        End Sub

    End Class
End Namespace
