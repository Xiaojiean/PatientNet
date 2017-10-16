/*
 * Copyright (c) Microsoft Corporation. All rights reserved. Licensed under the MIT license.
 * Borrowed from https://github.com/OfficeDev/skype-web-sdk-simple-sample-for-SfB-online
 */

var config = {
    /*
    The apiKey values in the below example are valid for the preview SDK.
    At general availability, these values will change.
    You can see apikeys information with the link below
    https://msdn.microsoft.com/en-us/skype/websdk/apiproductkeys
    */
    apiKey: 'a42fcebd-5b43-4b89-a065-74450fb91255', // You will use it in general purpose
    apiKeyCC: '9c967f6b-a846-4df2-b43d-5167e47d81e1', // You will use it when you use Conversation Control
    resource: 'https://webdir.online.lync.com', //Skype SDK resource.Fixed
    loginurl: 'https://login.microsoftonline.com/common/oauth2/authorize?response_type=token', //Microsoft OAuth login url for AAD.Fixed

    clientid: '16f92412-7c31-48e4-97c8-ed1625bc853d', //client id created in Azure AD
    docReplyurl: 'https://481patientnet.com/index.html', //replyurl you set in Azure AD
    clientReplyurl: 'https://481patientnet.com/client.html', //replyurl you set in Azure AD
    appName: 'skypewebsample' //Application Name registered in Azure AD
};
