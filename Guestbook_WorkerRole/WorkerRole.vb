' ----------------------------------------------------------------------------------
' Microsoft Developer & Platform Evangelism
' 
' Copyright (c) Microsoft Corporation. All rights reserved.
' 
' THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
' EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
' OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
' ----------------------------------------------------------------------------------
' The example companies, organizations, products, domain names,
' e-mail addresses, logos, people, places, and events depicted
' herein are fictitious.  No association with any real company,
' organization, product, domain name, email address, logo, person,
' places, or events is intended or should be inferred.
' ----------------------------------------------------------------------------------

Imports System.Threading
Imports Microsoft.WindowsAzure.Diagnostics
Imports Microsoft.WindowsAzure.ServiceRuntime

Imports Microsoft.WindowsAzure
Imports Microsoft.WindowsAzure.StorageClient
Imports System.IO
Imports GuestBook_Data
Imports System.Drawing

Public Class WorkerRole
    Inherits RoleEntryPoint

    Private Shared queue As CloudQueue
    Private Shared container As CloudBlobContainer

    Public Overrides Sub Run()
        Trace.TraceInformation("Listening for queue messages...")

        Do
            Try
                ' retrieve a new message from the queue
                Dim msg As CloudQueueMessage = queue.GetMessage()
                If msg IsNot Nothing Then
                    ' parse message retrieved from queue
                    Dim messageParts = msg.AsString.Split(New Char() {","c})
                    Dim uri = messageParts(0)
                    Dim partitionKey = messageParts(1)
                    Dim rowkey = messageParts(2)
                    Trace.TraceInformation("Processing image in blob '{0}'.", uri)

                    ' download original image from blob storage
                    Dim imageBlob As CloudBlockBlob = container.GetBlockBlobReference(uri)
                    Dim image As New MemoryStream()
                    imageBlob.DownloadToStream(image)
                    image.Seek(0, SeekOrigin.Begin)

                    ' create a thumbnail image and upload into a blob
                    Dim thumbnailUri As String = String.Concat(Path.GetFileNameWithoutExtension(uri), "_thumb.jpg")
                    Dim thumbnailBlob As CloudBlockBlob = container.GetBlockBlobReference(thumbnailUri)
                    thumbnailBlob.UploadFromStream(CreateThumbnail(image))

                    ' update the entry in table storage to point to the thumbnail
                    Dim ds = New GuestBookEntryDataSource()
                    ds.UpdateImageThumbnail(partitionKey, rowkey, thumbnailBlob.Uri.AbsoluteUri)

                    ' remove message from queue
                    queue.DeleteMessage(msg)

                    Trace.TraceInformation("Generated thumbnail in blob '{0}'.", thumbnailBlob.Uri)
                Else
                    System.Threading.Thread.Sleep(1000)
                End If
            Catch e As StorageClientException
                Trace.TraceError("Exception when processing queue item. Message: '{0}'", e.Message)
                System.Threading.Thread.Sleep(5000)
            End Try
        Loop
    End Sub

    Public Overrides Function OnStart() As Boolean

        '   Restart the role upon all configuration changes
        AddHandler RoleEnvironment.Changing, AddressOf RoleEnvironmentChanging

        ' read storage account configuration settings
        CloudStorageAccount.SetConfigurationSettingPublisher(Function(configName, configSetter) configSetter(RoleEnvironment.GetConfigurationSettingValue(configName)))
        Dim storageAccount = CloudStorageAccount.FromConfigurationSetting("DataConnectionString")

        ' initialize blob storage
        Dim blobStorage = storageAccount.CreateCloudBlobClient()
        container = blobStorage.GetContainerReference("guestbookpics")

        ' initialize queue storage 
        Dim queueStorage = storageAccount.CreateCloudQueueClient()
        queue = queueStorage.GetQueueReference("guestthumbs")

        Trace.TraceInformation("Creating container and queue...")

        Dim storageInitialized = False
        Do While (Not storageInitialized)
            Try
                ' create the blob container and allow public access
                container.CreateIfNotExist()
                Dim permissions = container.GetPermissions()
                permissions.PublicAccess = BlobContainerPublicAccessType.Container
                container.SetPermissions(permissions)

                ' create the message queue
                queue.CreateIfNotExist()
                storageInitialized = True
            Catch e As StorageClientException
                If (e.ErrorCode = StorageErrorCode.TransportError) Then

                    Trace.TraceError("Storage services initialization failure. " _
                      & "Check your storage account configuration settings. If running locally, " _
                      & "ensure that the Development Storage service is running. Message: '{0}'", e.Message)
                    System.Threading.Thread.Sleep(5000)
                Else
                    Throw
                End If
            End Try
        Loop
        Return MyBase.OnStart()

    End Function

    Private Sub RoleEnvironmentChanging(ByVal sender As Object, ByVal e As RoleEnvironmentChangingEventArgs)
        If (e.Changes.Any(Function(change) TypeOf change Is RoleEnvironmentConfigurationSettingChange)) Then
            e.Cancel = True
        End If
    End Sub

    Private Function CreateThumbnail(ByVal input As Stream) As Stream
        Dim orig As New Bitmap(input)
        Dim width As Integer
        Dim height As Integer

        If orig.Width > orig.Height Then
            width = 128
            height = 128 * orig.Height / orig.Width
        Else
            height = 128
            width = 128 * orig.Width / orig.Height
        End If
        Dim thumb As New Bitmap(width, height)

        Using graphic = Graphics.FromImage(thumb)
            graphic.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            graphic.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            graphic.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            graphic.DrawImage(orig, 0, 0, width, height)
            Dim ms As New MemoryStream()
            thumb.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg)
            ms.Seek(0, SeekOrigin.Begin)
            Return ms
        End Using
    End Function
End Class
