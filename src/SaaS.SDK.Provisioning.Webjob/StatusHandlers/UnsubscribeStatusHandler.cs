﻿using Microsoft.Marketplace.SaasKit.Client.DataAccess.Context;
using Microsoft.Marketplace.SaasKit.Contracts;
using Microsoft.Marketplace.SaasKit.Provisioning.Webjob;
using Microsoft.Marketplace.SaasKit.Provisioning.Webjob.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Marketplace.SaasKit.Client.DataAccess.Contracts;
using Microsoft.Marketplace.SaasKit.Client.DataAccess.Entities;
using Microsoft.Marketplace.SaasKit.Provisioning.Webjob.Helpers;

namespace Microsoft.Marketplace.SaasKit.Provisioning.Webjob.StatusHandlers
{

    class UnsubscribeStatusHandler : AbstractSubscriptionStatusHandler
    {

        private readonly IFulfillmentApiClient fulfillApiclient;
        private readonly IApplicationConfigRepository applicationConfigRepository;
        private readonly ISubscriptionsRepository subscriptionsRepository;
        private readonly ISubscriptionLogRepository subscriptionLogRepository;

        public UnsubscribeStatusHandler(IFulfillmentApiClient fulfillApiClient, IApplicationConfigRepository applicationConfigRepository, ISubscriptionsRepository subscriptionsRepository, ISubscriptionLogRepository subscriptionLogRepository) : base(new SaasKitContext())
        {
            this.fulfillApiclient = fulfillApiClient;
            this.applicationConfigRepository = applicationConfigRepository;
            this.subscriptionsRepository = subscriptionsRepository;
            this.subscriptionLogRepository = subscriptionLogRepository;


        }
        public override void Process(Guid subscriptionID)
        {
            Console.WriteLine("PendingActivationStatusHandler {0}", subscriptionID);
            var subscription = this.GetSubscriptionById(subscriptionID);
            Console.WriteLine("subscription : {0}", JsonConvert.SerializeObject(subscription));
            var deploymentStatus = Context.WebJobSubscriptionStatus.Where(s => s.SubscriptionId == subscriptionID).FirstOrDefault();
            Console.WriteLine("Get User");
            var userdeatils = this.GetUserById(subscription.UserId);


            if (subscription.SubscriptionStatus == SubscriptionWebJobStatusEnum.PendingUnsubscribe.ToString() ||
                subscription.SubscriptionStatus == SubscriptionWebJobStatusEnum.DeleteResourceSuccess.ToString()
                )
            {
                try
                {
                    var subscriptionData = this.fulfillApiclient.DeleteSubscriptionAsync(subscriptionID, subscription.AmpplanId).ConfigureAwait(false).GetAwaiter().GetResult();

                    this.subscriptionsRepository.UpdateStatusForSubscription(subscriptionID, SubscriptionWebJobStatusEnum.Unsubscribed.ToString(), true);


                    SubscriptionAuditLogs auditLog = new SubscriptionAuditLogs()
                    {
                        Attribute = SubscriptionLogAttributes.Status.ToString(),
                        SubscriptionId = subscription.Id,
                        NewValue = SubscriptionWebJobStatusEnum.Unsubscribed.ToString(),
                        OldValue = subscription.SubscriptionStatus,
                        CreateBy = userdeatils.UserId,
                        CreateDate = DateTime.Now
                    };
                    this.subscriptionLogRepository.Add(auditLog);
                }
                catch (Exception ex)
                {
                    string errorDescriptin = string.Format("Exception: {0} :: Innser Exception:{1}", ex.Message, ex.InnerException);
                    StatusUpadeHelpers.UpdateWebJobSubscriptionStatus(subscriptionID, default, DeploymentStatusEnum.DeleteResourceGroupSuccess.ToString(), errorDescriptin, Context, SubscriptionWebJobStatusEnum.UnsubscribeFailed.ToString());
                    Console.WriteLine(errorDescriptin);

                    // Activation Failure SubscriptionWebJobStatusEnum.ActivationFailed


                    this.subscriptionsRepository.UpdateStatusForSubscription(subscriptionID, SubscriptionWebJobStatusEnum.UnsubscribeFailed.ToString(), true);


                    SubscriptionAuditLogs auditLog = new SubscriptionAuditLogs()
                    {
                        Attribute = SubscriptionLogAttributes.Status.ToString(),
                        SubscriptionId = subscription.Id,
                        NewValue = SubscriptionWebJobStatusEnum.UnsubscribeFailed.ToString(),
                        OldValue = subscription.SubscriptionStatus,
                        CreateBy = userdeatils.UserId,
                        CreateDate = DateTime.Now
                    };
                    this.subscriptionLogRepository.Add(auditLog);


                    //Call Email helper with ActivationFailed
                }
            }
        }
    }

}

