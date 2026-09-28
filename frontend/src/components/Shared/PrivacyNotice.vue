<!--
  PrivacyNotice.vue — the Data Privacy Notice (Data Privacy Act of 2012,
  Republic Act No. 10173, as cited in the team's acceptance letter) shown before a parent/guardian's information is
  kept in Aruga. One text for every place that asks for consent:
    • staff registering a parent (Staff Patient Records, Admin pages)
    • the parent's first sign-in (ParentViews/Components/PrivacyConsentGate.vue)
  City Hall asked that it say plainly that the information is shared with
  the Aruga system and its developers, not only with the health center.
-->
<template>
  <div class="space-y-3 text-[12.5px] leading-relaxed text-slate-600">
    <p>
      <strong class="text-slate-800">Leveriza Health Center</strong> (Pasay City) keeps your and your child's
      vaccination information in <strong class="text-slate-800">Aruga</strong>, an online
      vaccination record system. Aruga was made for the health center by student developers,
      who keep it running. By agreeing, you allow the health center to share your information
      with Aruga and its developers for the purposes below.
    </p>

    <div>
      <p class="font-semibold text-slate-800">What we keep</p>
      <p>
        Parent/guardian: name, contact number, email, barangay and address, and your relationship
        to the child. Child: name, birth date, sex, place of birth, birth weight and length,
        Family No., allergies and existing conditions, and every vaccine given (date, batch and remarks).
      </p>
    </div>

    <div>
      <p class="font-semibold text-slate-800">Why</p>
      <p>
        To keep your child's immunization record, schedule the next vaccines, remind you by SMS and
        email, call you in at the clinic, and make reports for the health center (these reports show
        totals only, not names).
      </p>
    </div>

    <div>
      <p class="font-semibold text-slate-800">Who can see it</p>
      <p>
        Only authorized health center personnel (the Doctor and Nurses), and you for your own family.
        The developers may see it only when fixing or maintaining the system. Your information is not
        sold or given to anyone else, except when the law requires it (for example, Department of Health reports).
        Accounts are password-protected and every change is logged.
      </p>
    </div>

    <div>
      <p class="font-semibold text-slate-800">Your rights</p>
      <p>
        Under the Data Privacy Act of 2012 (Republic Act No. 10173) you may ask to see, correct, or have your information removed,
        object to its use, and file a complaint with the National Privacy Commission. Ask the health
        center staff to do any of these.
      </p>
    </div>
  </div>
</template>
