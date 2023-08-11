using PubNubUnityShowcase.UIComponents;
using UnityEngine;

namespace PubNubUnityShowcase
{
    /// <summary>
    /// Initiator Session Controller
    /// </summary>
    public class TradingViewStateInitiator : TradingViewStateBase,
        ITradeSessionSubscriber,
        ITradeInviteSubscriber
    {
        public TradingViewStateInitiator(TradeSessionData sessionData, TradingView.Services services, TradingView.UIComponents ui) : base(sessionData, services, ui)
        {
            Flow = new FlowCreateInitialOffer(sessionData, this, ui, services);
            Flow.Load();

            Services.Trading.SubscribeSessionEvents(this);
            Services.Trading.SubscribeTradeInvites(this);            
        }

        public override void Dispose()
        {
            base.Dispose();
            Services.Trading.UnsubscribeTradeInvites(this);
            Services.Trading.UnsubscribeSessionEvents(this);
        }

        #region ITradeSessionSubscriber
        void ITradeSessionSubscriber.OnParticipantJoined(TraderData participant)
        {

        }

        async void ITradeSessionSubscriber.OnParticipantGoodbyeAsync(LeaveSessionData leaveData)
        {
            Debug.LogError($"{leaveData.Participant.DisplayName} Left the trade: {leaveData.Reason}");

            if (leaveData.Reason == LeaveReason.withdrawOffer)
            {
                LeaveSessionData myLeave = new LeaveSessionData(SessionData.Initiator, LeaveReason.withdrawOffer);
                await Services.Trading.LeaveSessionAsync(myLeave); //You leave too

                Flow.Unload();
                Flow = new FlowSessionClosed("Offer Withdraw", SessionData, this, UI, Services);
                Flow.Load();
            }
        }

        void ITradeSessionSubscriber.OnLeftUnknownReason(TraderData participant)
        {

        }

        async void ITradeSessionSubscriber.OnTradingCompleted(OfferData offerData)
        {
            Flow.ReceivedOfferResponse = true;

            await Services.Trading.LeaveSessionAsync(new LeaveSessionData(SessionData.Initiator, LeaveReason.transactionComplete));

            if (offerData.State == OfferData.OfferState.accepted)
                StateSessionComplete($"Trade successfull");

            if (offerData.State == OfferData.OfferState.rejected)
                StateSessionComplete($"Trade Rejected");
        }

        void ITradeSessionSubscriber.OnCounterOffer(OfferData offerData)
        {
            Flow.ReceivedOfferResponse = true;

            Flow.Unload();
            Flow = new FlowOfferReceived(offerData, SessionData, this, UI, Services);
            Flow.Load();
        }

        #endregion

        #region ITradeInviteSubscriber
        void ITradeInviteSubscriber.OnTradeInviteReceived(TradeInvite invite)
        {
            //Send message that the user is busy trading (since this view can't be opened inGameOrLobby is asumed false)
            Services.Trading.InviteRespondAsync(invite, new InviteResponseData(false, true, false));
        }

        void ITradeInviteSubscriber.OnTradeInviteWithdrawn(TradeInvite invite)
        {
            //self close will be handled elsewhere
        }

        async void ITradeInviteSubscriber.OntradeInviteResponse(InviteResponseData response)
        {
            //only handle refuse to join (joins will handled as ITradeSessionSubscriber)
            Debug.Log($"Respondent: json={((IJsonSerializable)response).RawJson}");
            Flow.TradeInviteResponceReceived = true;

            if (response.WillJoin == false)
            {
                //why?
                if (response.InGameOrLobby)
                {
                    StateSessionComplete($"{SessionData.Respondent.DisplayName} is in Match");
                    await Services.Trading.LeaveSessionAsync(new LeaveSessionData(SessionData.Initiator, LeaveReason.otherPartyBusy));
                }

                else if (response.InTradingSession)
                {
                    StateSessionComplete($"{SessionData.Respondent.DisplayName} is Trading with another player");
                    await Services.Trading.LeaveSessionAsync(new LeaveSessionData(SessionData.Initiator, LeaveReason.otherPartyBusy));
                }
            }
            else
            {
                UI.OfferPanel.SetSessionStatus($"{SessionData.Respondent.DisplayName} checking your offer");
            }
        }
        #endregion
    }
}