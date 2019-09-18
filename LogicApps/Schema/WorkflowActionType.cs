using System;
using System.Collections.Generic;
using System.Text;

namespace LogicApps.Schema
{
    internal enum WorkflowActionType
    {
        None = 0,
        ApiConnection,
        ApiConnectionWebhook,
        ApiManagement,
        AppendToArrayVariable,
        AppendToStringVariable,
        Batch,
        Compose,
        DecrementVariable,
        Expression,
        FlatFileDecoding,
        FlatFileEncoding,
        Foreach,
        Function,
        Http,
        HttpWebhook,
        If,
        IncrementVariable,
        InitializeVariable,
        IntegrationAccountArtifactLookup,
        Join,
        Liquid,
        ParseJson,
        Query,
        Recurrence,
        Request,
        Response,
        Scope,
        Select,
        SendToBatch,
        SetVariable,
        SlidingWindow,
        Switch,
        Table,
        Terminate,
        Until,
        Wait,
        Workflow,
        XmlValidation,
        Xslt,
    }
}
