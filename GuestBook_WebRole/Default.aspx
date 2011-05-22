<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="GuestBook_WebRole._Default" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head id="Head1" runat="server">
    <title>Windows Azure Guestbook</title>
    <link href="main.css" rel="stylesheet" type="text/css" />
</head>
<body>
    <form id="form1" runat="server">
    <asp:ScriptManager ID="ScriptManager1" runat="server">
    </asp:ScriptManager>
    <div class="general">
        <div class="title">
            <h1>
                Windows Azure GuestBook
            </h1>
        </div>
        <div class="inputSection">
            <dl>
                <dt>
                    <label for="NameLabel">Name:</label></dt>
                <dd>
                    <asp:TextBox 
                       ID="NameTextBox" 
                       runat="server" 
                       CssClass="field"/>
                    <asp:RequiredFieldValidator 
                      ID="NameRequiredValidator" 
                      runat="server" 
                      ControlToValidate="NameTextBox"
                      Text="*" />
                </dd>
                <dt>
                    <label for="MessageLabel">Message:</label>
                </dt>
                <dd>
                    <asp:TextBox 
                       ID="MessageTextBox" 
                       runat="server" 
                       TextMode="MultiLine" 
                       CssClass="field" />
                    <asp:RequiredFieldValidator 
                       ID="MessageRequiredValidator" 
                       runat="server" 
                       ControlToValidate="MessageTextBox"
                       Text="*" />
                </dd>
                <dt>
                    <label for="FileUpload1">Photo:</label></dt>
                <dd>
                    <asp:FileUpload 
                        ID="FileUpload1" 
                        runat="server" 
                        size="16" />
                    <asp:RequiredFieldValidator 
                        ID="PhotoRequiredValidator" 
                        runat="server" 
                        ControlToValidate="FileUpload1"
                        Text="*" />
                    <asp:RegularExpressionValidator 
                        ID="PhotoRegularExpressionValidator"
                        runat="server"
                        ControlToValidate="FileUpload1" 
                        ErrorMessage="Only .jpg or .png files are allowed"
                        ValidationExpression="([a-zA-Z\\].*(.jpg|.JPG|.png|.PNG)$)" />
                </dd>
            </dl>
            <div class="inputSignSection">
                <asp:ImageButton 
                       ID="SignButton" 
                       runat="server" 
                       AlternateText="Sign GuestBook"
                       onclick="SignButton_Click" 
                       ImageUrl="~/sign.png" 
                       ImageAlign="Bottom"  />
            </div>
        </div>
        <asp:UpdatePanel ID="UpdatePanel1" runat="server">
            <ContentTemplate>
                <asp:DataList 
                   ID="DataList1" 
                   runat="server" 
                   DataSourceID="ObjectDataSource1">
                    <ItemTemplate>
                        <div class="signature">
                            <div class="signatureImage">
                                <a href="<%# Eval("PhotoUrl") %>" target="_blank">
                                    <img src="<%# Eval("ThumbnailUrl") %>" 
                                         alt="<%# Eval("GuestName") %>" />
                                </a>
                            </div>
                            <div class="signatureDescription">
                                <div class="signatureName">
                                    <%# Eval("GuestName") %>
                                </div>
                                <div class="signatureSays">
                                    says
                                </div>
                                <div class="signatureDate">
                                    <%# ((DateTime)Eval("Timestamp")).ToShortDateString() %>
                                </div>
                                <div class="signatureMessage">
                                    "<%# Eval("Message") %>"
                                </div>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:DataList>
                <asp:Timer 
                    ID="Timer1" 
                    runat="server"
                    Interval="15000"
                    OnTick="Timer1_Tick">
                </asp:Timer>
            </ContentTemplate>
        </asp:UpdatePanel>
        <asp:ObjectDataSource 
           ID="ObjectDataSource1"
           runat="server" 
           DataObjectTypeName="GuestBook_Data.GuestBookEntry"
           InsertMethod="AddGuestBookEntry"
           SelectMethod="Select" 
           TypeName="GuestBook_Data.GuestBookEntryDataSource">
        </asp:ObjectDataSource>
    </div>
    </form>
</body>
</html>
