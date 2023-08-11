using PubNubUnityShowcase.UIComponents;
using System;
using UnityEngine;

namespace PubNubUnityShowcase
{
    /// <summary>
    /// Respondent Session Controller
    /// </summary>
    public class TradingViewStateRespondent : TradingViewStateBase,
        ITradeSessionSubscriber
    {
        public TradingViewStateRespondent(TradeSessionData sessionData, OfferData originalOffer, TradingView.Services services, TradingView.UIComponents ui) : base(sessionData, services, ui)
        {
            Flow = new FlowOfferReceived(originalOffer, sessionData, this, ui, services);
            Flow.Load();
            Services.Trading.SubscribeSessionEvents(this);
        }

        public override void Dispose()
        {
            base.Dispose();
            Services.Trading.UnsubscribeSessionEvents(this);
        }

        #region ITradeSessionSubscriber
        void ITradeSessionSubscriber.OnParticipantJoined(TraderData participant)
        {
            Debug.LogWarning("Respondent should always join second. Something went wrong");
        }

        async void ITradeSessionSubscriber.OnParticipantGoodbye(LeaveSessionData leaveData)
        {
            Debug.Log($"{leaveData.Participant.DisplayName} Left the trade: {leaveData.Reason}");

            if (leaveData.Reason == LeaveReason.withdrawOffer)
            {
                LeaveSessionData myLeave = new LeaveSessionData(SessionData.Initiator, LeaveReason.withdrawOffer);
                await Services.Trading.LeaveSessionAsync(myLeave); //You leave too
                StateSessionComplete($"Offer Withdraw");
            }
        }

        void ITradeSessionSubscriber.OnLeftUnknownReason(TraderData participant)
        {
            Debug.Log("LeftUnknown");
        }

        async void ITradeSessionSubscriber.OnTradingCompleted(OfferData offerData)
        {
            await Services.Trading.LeaveSessionAsync(new LeaveSessionData(SessionData.Initiator, LeaveReason.transactionComplete));
            if (offerData.State == OfferData.OfferState.accepted)
                StateSessionComplete($"Trade successfull");

            if (offerData.State == OfferData.OfferState.rejected)
                StateSessionComplete($"Trade Rejected");
        }

        void ITradeSessionSubscriber.OnCounterOffer(OfferData offerData)
        {
            Debug.LogWarning("------------>Received Counteroffer");

            offerPanel.SetLabel("counter offer");
        }
        #endregion


    }
}